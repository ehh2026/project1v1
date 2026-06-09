// Shaft join/axis analysis modes: --find-join, --fit-axis, --measure-shaft.
using System.Drawing;
using System.Text.Json;

internal static class JoinAnalysis
{
    internal static void FindJoin(JsonElement pin, string pinId, string partsDir, string cleanedDir)
    {
        var baseFile = pin.GetProperty("shaft_file").GetString()!;
        var shaft    = pin.GetProperty("shaft");
        var tip      = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_tip"));
        var curJoin  = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_join"));
        var curNativeLen = shaft.GetProperty("native_length").GetDouble();
        var angleDeg = shaft.GetProperty("native_angle_deg").GetDouble();

        double rad  = angleDeg * Math.PI / 180.0;
        float  axDx = (float)Math.Sin(rad);
        float  axDy = (float)-Math.Cos(rad);

        var (srcPath, srcLabel) = ShaftPixelSampler.ResolveShaftPath(pinId, baseFile, partsDir, cleanedDir);
        if (!File.Exists(srcPath))
        {
            Console.WriteLine($"  {pinId}: !! missing {srcPath}");
            return;
        }

        var sample = ShaftPixelSampler.TryRead(srcPath);
        if (sample == null || sample.OpaqueCount == 0)
        {
            Console.WriteLine($"  {pinId}: no opaque pixels found");
            return;
        }

        var projections = new List<(float proj, float x, float y)>(sample.OpaqueCount);
        foreach (var (px, py) in sample.OpaquePixels)
        {
            float dx   = px - tip.X, dy = py - tip.Y;
            float proj = dx * axDx + dy * axDy;
            projections.Add((proj, px, py));
        }

        projections.Sort((a, b) => a.proj.CompareTo(b.proj));

        int startIdx = (int)(projections.Count * 0.97);
        float sumX = 0, sumY = 0;
        int   cnt  = 0;
        for (int i = startIdx; i < projections.Count; i++)
        {
            sumX += projections[i].x;
            sumY += projections[i].y;
            cnt++;
        }
        float newJoinX = sumX / cnt;
        float newJoinY = sumY / cnt;

        float newLen    = (float)Math.Sqrt(Math.Pow(newJoinX - tip.X, 2) + Math.Pow(newJoinY - tip.Y, 2));
        float curEucLen = (float)Math.Sqrt(Math.Pow(curJoin.X - tip.X, 2) + Math.Pow(curJoin.Y - tip.Y, 2));
        bool  changed   = Math.Abs(newJoinX - curJoin.X) > 5 || Math.Abs(newJoinY - curJoin.Y) > 5;

        Console.WriteLine($"  {pinId}  [{srcLabel}]  angle={angleDeg:F1}°  img={sample.Width}x{sample.Height}");
        Console.WriteLine($"    current join : ({curJoin.X,7:F1}, {curJoin.Y,7:F1})  euclidean={curEucLen,6:F1}  json_native={curNativeLen:F1}");
        Console.WriteLine($"    suggested    : ({newJoinX,7:F1}, {newJoinY,7:F1})  euclidean={newLen,6:F1}{(changed ? "  ← CHANGED" : "")}");
        double newAngleDeg = Math.Atan2(newJoinX - tip.X, -(newJoinY - tip.Y)) * 180.0 / Math.PI;
        if (newAngleDeg < 0) newAngleDeg += 360.0;
        bool angleChanged = Math.Abs(newAngleDeg - angleDeg) > 0.5;

        if (changed)
        {
            Console.WriteLine($"    JSON patch   : \"local_join\": {{ \"x\": {newJoinX:F1}, \"y\": {newJoinY:F1} }},  \"native_length\": {newLen:F1},  \"native_angle_deg\": {newAngleDeg:F1}");
            if (angleChanged)
                Console.WriteLine($"    angle change : {angleDeg:F1}° → {newAngleDeg:F1}°  (Δ={newAngleDeg - angleDeg:+0.0;-0.0}°)");
        }
        Console.WriteLine();
    }

    internal static void FitAxis(JsonElement pin, string pinId, string partsDir, string cleanedDir)
    {
        var baseFile = pin.GetProperty("shaft_file").GetString()!;
        var shaft    = pin.GetProperty("shaft");
        var tip      = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_tip"));
        var curJoin  = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_join"));
        double curNativeLen = shaft.GetProperty("native_length").GetDouble();
        double curAngle    = shaft.GetProperty("native_angle_deg").GetDouble();

        var (srcPath, srcLabel) = ShaftPixelSampler.ResolveShaftPath(pinId, baseFile, partsDir, cleanedDir);
        if (!File.Exists(srcPath)) { Console.WriteLine($"  {pinId}: !! missing {srcPath}"); return; }

        var sample = ShaftPixelSampler.TryRead(srcPath);
        if (sample == null || sample.OpaqueCount < 10)
        {
            Console.WriteLine($"  {pinId}: too few opaque pixels");
            return;
        }

        double sumX = 0, sumY = 0;
        foreach (var (px, py) in sample.OpaquePixels) { sumX += px; sumY += py; }
        int cnt = sample.OpaqueCount;
        double cx = sumX / cnt, cy = sumY / cnt;

        double cxx = 0, cyy = 0, cxy = 0;
        foreach (var (px, py) in sample.OpaquePixels)
        {
            double dx = px - cx, dy = py - cy;
            cxx += dx * dx; cyy += dy * dy; cxy += dx * dy;
        }
        cxx /= cnt; cyy /= cnt; cxy /= cnt;

        double halfTrace = (cxx + cyy) / 2.0;
        double disc      = Math.Sqrt(Math.Max(0, (cxx - cyy) * (cxx - cyy) / 4.0 + cxy * cxy));
        double lam1      = halfTrace + disc;
        double evx, evy;
        if (Math.Abs(cxy) > 1e-6)
        {
            evx = cxy; evy = lam1 - cxx;
        }
        else
        {
            evx = cxx >= cyy ? 1.0 : 0.0;
            evy = cxx >= cyy ? 0.0 : 1.0;
        }
        double evLen = Math.Sqrt(evx * evx + evy * evy);
        evx /= evLen; evy /= evLen;

        double jtDx = curJoin.X - tip.X, jtDy = curJoin.Y - tip.Y;
        if (evx * jtDx + evy * jtDy < 0) { evx = -evx; evy = -evy; }

        var projs = new List<(float proj, float px, float py)>(cnt);
        foreach (var (px, py) in sample.OpaquePixels)
        {
            float proj = (float)((px - tip.X) * evx + (py - tip.Y) * evy);
            if (proj > 0) projs.Add((proj, px, py));
        }
        projs.Sort((a, b) => a.proj.CompareTo(b.proj));

        int startIdx = (int)(projs.Count * 0.97);
        float sjx = 0, sjy = 0; int sc = 0;
        for (int i = startIdx; i < projs.Count; i++) { sjx += projs[i].px; sjy += projs[i].py; sc++; }
        if (sc == 0) { Console.WriteLine($"  {pinId}: no forward pixels"); return; }

        float newJoinX = sjx / sc;
        float newJoinY = sjy / sc;
        float newLen   = MathF.Sqrt((newJoinX - tip.X) * (newJoinX - tip.X) +
                                    (newJoinY - tip.Y) * (newJoinY - tip.Y));
        double newAngle = Math.Atan2(newJoinX - tip.X, -(newJoinY - tip.Y)) * 180.0 / Math.PI;
        if (newAngle < 0) newAngle += 360.0;

        double pcaAngle = Math.Atan2(evx, -evy) * 180.0 / Math.PI;
        if (pcaAngle < 0) pcaAngle += 360.0;

        bool joinChanged  = Math.Abs(newJoinX - curJoin.X) > 2 || Math.Abs(newJoinY - curJoin.Y) > 2;
        bool angleChanged = Math.Abs(newAngle - curAngle) > 0.5;

        Console.WriteLine($"  {pinId}  [{srcLabel}]  img={sample.Width}x{sample.Height}");
        Console.WriteLine($"    current   : join=({curJoin.X:F1}, {curJoin.Y:F1})  native={curNativeLen:F1}  angle={curAngle:F1}°");
        Console.WriteLine($"    PCA axis  : ({evx:F4}, {evy:F4})  angle={pcaAngle:F1}°  (λ ratio={lam1 / (halfTrace - disc + 1e-9):F1}x)");
        Console.WriteLine($"    new join  : ({newJoinX:F1}, {newJoinY:F1})  native={newLen:F1}  angle={newAngle:F1}°{(joinChanged ? "  ← CHANGED" : "")}");
        if (joinChanged || angleChanged)
            Console.WriteLine($"    JSON patch: \"local_join\": {{ \"x\": {newJoinX:F1}, \"y\": {newJoinY:F1} }},  \"native_length\": {newLen:F1},  \"native_angle_deg\": {newAngle:F1}");
        Console.WriteLine();
    }

    internal static void MeasureShaft(JsonElement pin, string pinId, string partsDir, string cleanedDir)
    {
        var baseFile = pin.GetProperty("shaft_file").GetString()!;
        var shaft    = pin.GetProperty("shaft");
        var tip      = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_tip"));
        var join     = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_join"));
        var seg      = shaft.GetProperty("segmentation");
        double strStart = seg.GetProperty("stretch_start_distance").GetDouble();
        double strEnd   = seg.GetProperty("stretch_end_distance").GetDouble();

        float aDx = join.X - tip.X, aDy = join.Y - tip.Y;
        float aLen = MathF.Sqrt(aDx * aDx + aDy * aDy);
        if (aLen < 0.1f) { Console.WriteLine($"  {pinId}: degenerate axis"); return; }
        aDx /= aLen; aDy /= aLen;
        float nNx = -aDy, nNy = aDx;

        var (srcPath, srcLabel) = ShaftPixelSampler.ResolveShaftPath(pinId, baseFile, partsDir, cleanedDir);
        if (!File.Exists(srcPath)) { Console.WriteLine($"  {pinId}: !! missing {srcPath}"); return; }

        var sample = ShaftPixelSampler.TryRead(srcPath);
        if (sample == null) return;

        var dists = new List<float>(sample.OpaqueCount);
        int bodyCount = 0;
        foreach (var (px, py) in sample.OpaquePixels)
        {
            float dx   = px - tip.X, dy = py - tip.Y;
            float axial = dx * aDx + dy * aDy;
            if (axial < (float)strStart || axial > (float)strEnd) continue;
            bodyCount++;
            dists.Add(MathF.Abs(dx * nNx + dy * nNy));
        }

        if (dists.Count == 0) { Console.WriteLine($"  {pinId}: no body-region pixels"); return; }

        dists.Sort();
        float halfW = dists[dists.Count / 2];

        double curVal = shaft.TryGetProperty("native_shaft_half_width_px", out var cur)
                        ? cur.GetDouble() : 0.0;
        bool changed = Math.Abs(halfW - curVal) > 0.5;

        Console.WriteLine($"  {pinId}  [{srcLabel}]  img={sample.Width}x{sample.Height}  body_pixels={bodyCount}");
        Console.WriteLine($"    current  native_shaft_half_width_px : {curVal:F1}");
        Console.WriteLine($"    measured (body-region median perp)  : {halfW:F1}{(changed ? "  ← CHANGED" : "")}");
        if (changed)
            Console.WriteLine($"    JSON patch: \"native_shaft_half_width_px\": {halfW:F1}");
        Console.WriteLine();
    }
}
