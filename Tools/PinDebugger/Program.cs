// Pin geometry debug annotator + shadow cleaner + join finder.
// Run from the project root.
//
// Annotate mode (default):
//   dotnet run --project Tools\PinDebugger [partsDir [outputDir]]
//   Draws calibration markers on head/shaft/shaft_lit PNG copies.
//
// Clean mode:
//   dotnet run --project Tools\PinDebugger -- --clean [partsDir [cleanedDir]]
//   Removes disconnected pixel islands (e.g. cast head-ball shadows) from
//   each shaft PNG by keeping only the connected component that contains
//   local_tip.  Output files: pin_XX_shaft_clean.png + pin_XX_shaft_lit_clean.png
//   Copy them over the originals once you have verified the results.
//
// Find-join mode:
//   dotnet run --project Tools\PinDebugger -- --find-join [partsDir [cleanedDir]]
//   For each shaft, uses native_angle_deg to project all non-transparent pixels
//   onto the shaft axis and reports the centroid of the farthest 3% as the
//   suggested new local_join.  Use the cleaned versions if available.
//   Outputs a JSON patch snippet ready to paste into pin_part_geometry.json.
//
// Fit-axis mode:
//   dotnet run --project Tools\PinDebugger -- --fit-axis [partsDir [cleanedDir]]
//   Uses PCA on the shaft pixel distribution to find the principal axis direction
//   independently of native_angle_deg, then re-estimates local_join along that axis.
//   Outputs JSON patch snippets with corrected local_join, native_length, native_angle_deg.
//
// Composites mode:
//   dotnet run --project Tools\PinDebugger -- --composites [partsDir [outputDir]]
//   Renders each pin as a grid of composites: 8 target angles × 4 target lengths.
//   Each cell shows shaft + head composited together with cyan tip / yellow join dots.
//   Add --lit to use the _lit shaft variants.
//   Output: Tools/PinDebugger/composites/{pinId}_composites.png

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

bool cleanMode      = args.Any(a => a == "--clean");
bool findJoinMode   = args.Any(a => a == "--find-join");
bool fitAxisMode    = args.Any(a => a == "--fit-axis");
bool compositesMode = args.Any(a => a == "--composites");
bool useLitShafts   = args.Any(a => a == "--lit");
var  posArgs        = args.Where(a => !a.StartsWith("--")).ToArray();

var partsDir   = posArgs.Length > 0 ? posArgs[0] : Path.Combine("Images&Content", "Pins_v2", "parts");
var cleanedDir = Path.Combine("Tools", "PinDebugger", "cleaned"); // always checked for cleaned versions
var outputDir  = posArgs.Length > 1 ? posArgs[1]
    : cleanMode      ? cleanedDir
    : findJoinMode   ? Path.Combine("Tools", "PinDebugger", "find-join")
    : fitAxisMode    ? Path.Combine("Tools", "PinDebugger", "find-join")  // shares output dir
    : compositesMode ? Path.Combine("Tools", "PinDebugger", "composites")
    : Path.Combine("Tools", "PinDebugger", "output_v2");

Directory.CreateDirectory(outputDir);
Console.WriteLine($"Mode   : {(cleanMode ? "clean" : findJoinMode ? "find-join" : fitAxisMode ? "fit-axis" : compositesMode ? $"composites{(useLitShafts ? " --lit" : "")}" : "annotate")}");
Console.WriteLine($"Parts  : {partsDir}");
Console.WriteLine($"Output : {outputDir}");
Console.WriteLine();

var raw  = File.ReadAllBytes(Path.Combine(partsDir, "pin_part_geometry.json"));
int bom  = (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) ? 3 : 0;
var json = Encoding.UTF8.GetString(raw, bom, raw.Length - bom);
using var doc = JsonDocument.Parse(json);

if (compositesMode)
{
    RunComposites(doc.RootElement, partsDir, outputDir, useLitShafts);
    Console.WriteLine();
    Console.WriteLine("Done.");
}
else
{
    int count = 0;
    foreach (var pinProp in doc.RootElement.EnumerateObject())
    {
        var pinId = pinProp.Name;
        var pin   = pinProp.Value;

        if (cleanMode)
        {
            Console.WriteLine($"  {pinId}");
            CleanShaft(pin, pinId, partsDir, outputDir, litSuffix: false);
            CleanShaft(pin, pinId, partsDir, outputDir, litSuffix: true);
        }
        else if (findJoinMode)
        {
            FindJoin(pin, pinId, partsDir, cleanedDir);
        }
        else if (fitAxisMode)
        {
            FitAxis(pin, pinId, partsDir, cleanedDir);
        }
        else
        {
            Console.WriteLine($"  {pinId}");
            AnnotateHead (pin, pinId, partsDir, outputDir);
            AnnotateShaft(pin, pinId, partsDir, outputDir, litSuffix: false);
            AnnotateShaft(pin, pinId, partsDir, outputDir, litSuffix: true);
        }
        count++;
    }

    Console.WriteLine();
    Console.WriteLine($"Done — {count} pins processed.");
}

// ── Clean mode ────────────────────────────────────────────────────────────────

static void CleanShaft(JsonElement pin, string pinId, string partsDir, string outputDir, bool litSuffix)
{
    var baseFile  = pin.GetProperty("shaft_file").GetString()!;
    var shaftFile = litSuffix ? baseFile.Replace(".png", "_lit.png") : baseFile;
    var srcPath   = Path.Combine(partsDir, shaftFile);
    if (!File.Exists(srcPath)) return;

    var shaft = pin.GetProperty("shaft");
    var tipPt = ParsePoint(shaft.GetProperty("local_tip"));
    int seedX = (int)Math.Round(tipPt.X);
    int seedY = (int)Math.Round(tipPt.Y);

    using var orig    = new Bitmap(srcPath);
    using var cleaned = KeepConnectedComponent(orig, seedX, seedY);

    var outName = litSuffix ? $"{pinId}_shaft_lit_clean.png" : $"{pinId}_shaft_clean.png";
    var outPath = Path.Combine(outputDir, outName);
    cleaned.Save(outPath, ImageFormat.Png);
    Console.WriteLine($"    → {outPath}");
}

/// <summary>
/// Returns a copy of <paramref name="src"/> with all pixels zeroed except those
/// in the 8-connected non-transparent component that contains the seed pixel.
/// If the seed is transparent, the nearest non-transparent pixel within 30 px is used.
/// </summary>
static Bitmap KeepConnectedComponent(Bitmap src, int seedX, int seedY)
{
    const int AlphaThreshold = 1; // treat alpha=0 as background, anything else as foreground

    int w = src.Width, h = src.Height;
    var rect    = new Rectangle(0, 0, w, h);
    var srcData = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    int stride  = srcData.Stride;
    var pixels  = new byte[stride * h];
    System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, pixels, 0, pixels.Length);
    src.UnlockBits(srcData);

    byte GetA(int x, int y) => pixels[(y * stride) + (x * 4) + 3];

    // Clamp seed to image bounds
    int sx = Math.Max(0, Math.Min(w - 1, seedX));
    int sy = Math.Max(0, Math.Min(h - 1, seedY));

    // If the seed pixel is transparent (e.g. the pin tip is a single near-transparent
    // pixel), search outward in expanding rings to find the nearest opaque pixel.
    if (GetA(sx, sy) < AlphaThreshold)
    {
        bool found = false;
        for (int r = 1; r <= 30 && !found; r++)
        for (int dy = -r; dy <= r && !found; dy++)
        for (int dx = -r; dx <= r && !found; dx++)
        {
            if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // perimeter only
            int nx = sx + dx, ny = sy + dy;
            if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
            if (GetA(nx, ny) >= AlphaThreshold) { sx = nx; sy = ny; found = true; }
        }
    }

    // BFS 8-connected flood fill from seed
    var keep  = new bool[w * h];
    var queue = new Queue<int>();

    if (GetA(sx, sy) >= AlphaThreshold)
    {
        int start = sy * w + sx;
        keep[start] = true;
        queue.Enqueue(start);
    }

    while (queue.Count > 0)
    {
        int idx = queue.Dequeue();
        int x   = idx % w;
        int y   = idx / w;

        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            int nx = x + dx, ny = y + dy;
            if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
            int ni = ny * w + nx;
            if (!keep[ni] && GetA(nx, ny) >= AlphaThreshold)
            {
                keep[ni] = true;
                queue.Enqueue(ni);
            }
        }
    }

    // Build output — copy kept pixels verbatim, leave others as transparent zero
    var result  = new Bitmap(w, h, PixelFormat.Format32bppArgb);
    var dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
    int dstStride = dstData.Stride;
    var outPx   = new byte[dstStride * h]; // zero-initialised = fully transparent

    for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
    {
        if (!keep[y * w + x]) continue;
        int si = (y * stride)    + (x * 4);
        int di = (y * dstStride) + (x * 4);
        outPx[di]     = pixels[si];
        outPx[di + 1] = pixels[si + 1];
        outPx[di + 2] = pixels[si + 2];
        outPx[di + 3] = pixels[si + 3];
    }

    System.Runtime.InteropServices.Marshal.Copy(outPx, 0, dstData.Scan0, outPx.Length);
    result.UnlockBits(dstData);
    return result;
}

// ── Find-join mode ────────────────────────────────────────────────────────────

/// <summary>
/// Uses native_angle_deg to project all non-transparent shaft pixels onto the
/// shaft axis and reports the centroid of the farthest 3% as the suggested
/// new local_join.  Uses the cleaned shaft version if available.
/// </summary>
static void FindJoin(JsonElement pin, string pinId, string partsDir, string cleanedDir)
{
    var baseFile = pin.GetProperty("shaft_file").GetString()!;
    var shaft    = pin.GetProperty("shaft");
    var tip      = ParsePoint(shaft.GetProperty("local_tip"));
    var curJoin  = ParsePoint(shaft.GetProperty("local_join"));
    var curNativeLen = shaft.GetProperty("native_length").GetDouble();
    var angleDeg = shaft.GetProperty("native_angle_deg").GetDouble();

    // Axis unit vector from native_angle_deg.
    // Convention (matches GetAngleDegrees in the app): 0° = up (–Y), 90° = right (+X).
    double rad  = angleDeg * Math.PI / 180.0;
    float  axDx = (float)Math.Sin(rad);
    float  axDy = (float)-Math.Cos(rad); // screen Y increases downward

    // Prefer cleaned version; fall back to original in parts dir
    var cleanedPath  = Path.Combine(cleanedDir, $"{pinId}_shaft_clean.png");
    var originalPath = Path.Combine(partsDir, baseFile);
    var srcPath      = File.Exists(cleanedPath) ? cleanedPath : originalPath;
    var srcLabel     = File.Exists(cleanedPath) ? "cleaned" : "original";

    if (!File.Exists(srcPath))
    {
        Console.WriteLine($"  {pinId}: !! missing {srcPath}");
        return;
    }

    // Collect projections of all sufficiently-opaque pixels onto the axis
    using var bmp    = new Bitmap(srcPath);
    int w = bmp.Width, h = bmp.Height;
    var rect    = new Rectangle(0, 0, w, h);
    var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    int stride  = bmpData.Stride;
    var pixels  = new byte[stride * h];
    Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);
    bmp.UnlockBits(bmpData);

    var projections = new List<(float proj, float x, float y)>(w * h / 4);
    for (int py = 0; py < h; py++)
    for (int px = 0; px < w; px++)
    {
        if (pixels[(py * stride) + (px * 4) + 3] < 10) continue;
        float dx   = px - tip.X, dy = py - tip.Y;
        float proj = dx * axDx + dy * axDy;
        projections.Add((proj, px, py));
    }

    if (projections.Count == 0)
    {
        Console.WriteLine($"  {pinId}: no opaque pixels found");
        return;
    }

    projections.Sort((a, b) => a.proj.CompareTo(b.proj));

    // Centroid of the farthest 3% of pixels (by axis projection)
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

    // Euclidean distance tip → new join
    float newLen    = (float)Math.Sqrt(Math.Pow(newJoinX - tip.X, 2) + Math.Pow(newJoinY - tip.Y, 2));
    float curEucLen = (float)Math.Sqrt(Math.Pow(curJoin.X - tip.X, 2) + Math.Pow(curJoin.Y - tip.Y, 2));
    bool  changed   = Math.Abs(newJoinX - curJoin.X) > 5 || Math.Abs(newJoinY - curJoin.Y) > 5;

    Console.WriteLine($"  {pinId}  [{srcLabel}]  angle={angleDeg:F1}°  img={w}x{h}");
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

// ── Fit-axis mode ──────────────────────────────────────────────────────────────

/// <summary>
/// Uses PCA on the shaft pixel distribution to find the principal axis direction,
/// then re-estimates local_join along that axis (same 3% farthest centroid as find-join).
/// Unlike find-join, does NOT assume native_angle_deg is correct — the axis direction
/// is derived entirely from the pixel data.
/// </summary>
static void FitAxis(JsonElement pin, string pinId, string partsDir, string cleanedDir)
{
    var baseFile = pin.GetProperty("shaft_file").GetString()!;
    var shaft    = pin.GetProperty("shaft");
    var tip      = ParsePoint(shaft.GetProperty("local_tip"));
    var curJoin  = ParsePoint(shaft.GetProperty("local_join"));
    double curNativeLen = shaft.GetProperty("native_length").GetDouble();
    double curAngle    = shaft.GetProperty("native_angle_deg").GetDouble();

    var cleanedPath  = Path.Combine(cleanedDir, $"{pinId}_shaft_clean.png");
    var originalPath = Path.Combine(partsDir, baseFile);
    var srcPath      = File.Exists(cleanedPath) ? cleanedPath : originalPath;
    var srcLabel     = File.Exists(cleanedPath) ? "cleaned" : "original";
    if (!File.Exists(srcPath)) { Console.WriteLine($"  {pinId}: !! missing {srcPath}"); return; }

    using var bmp = new Bitmap(srcPath);
    int w = bmp.Width, h = bmp.Height;
    var rect    = new Rectangle(0, 0, w, h);
    var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    int stride  = bmpData.Stride;
    var pixels  = new byte[stride * h];
    Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);
    bmp.UnlockBits(bmpData);

    // Centroid of all opaque pixels
    double sumX = 0, sumY = 0;
    int    cnt  = 0;
    for (int py = 0; py < h; py++)
    for (int px = 0; px < w; px++)
    {
        if (pixels[py * stride + px * 4 + 3] < 10) continue;
        sumX += px; sumY += py; cnt++;
    }
    if (cnt < 10) { Console.WriteLine($"  {pinId}: too few opaque pixels"); return; }
    double cx = sumX / cnt, cy = sumY / cnt;

    // 2×2 covariance matrix of pixel coordinates
    double cxx = 0, cyy = 0, cxy = 0;
    for (int py = 0; py < h; py++)
    for (int px = 0; px < w; px++)
    {
        if (pixels[py * stride + px * 4 + 3] < 10) continue;
        double dx = px - cx, dy = py - cy;
        cxx += dx * dx; cyy += dy * dy; cxy += dx * dy;
    }
    cxx /= cnt; cyy /= cnt; cxy /= cnt;

    // Principal eigenvector of [[cxx, cxy], [cxy, cyy]].
    // Larger eigenvalue: λ₁ = (cxx+cyy)/2 + sqrt(((cxx-cyy)/2)² + cxy²)
    // Corresponding eigenvector: [cxy, λ₁ - cxx]  (or [1,0]/[0,1] when cxy≈0)
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

    // Orient eigenvector to match the current tip→join direction
    double jtDx = curJoin.X - tip.X, jtDy = curJoin.Y - tip.Y;
    if (evx * jtDx + evy * jtDy < 0) { evx = -evx; evy = -evy; }

    // Re-estimate join: centroid of farthest 3% projected onto PCA axis
    var projs = new List<(float proj, float px, float py)>(cnt);
    for (int py = 0; py < h; py++)
    for (int px = 0; px < w; px++)
    {
        if (pixels[py * stride + px * 4 + 3] < 10) continue;
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

    Console.WriteLine($"  {pinId}  [{srcLabel}]  img={w}x{h}");
    Console.WriteLine($"    current   : join=({curJoin.X:F1}, {curJoin.Y:F1})  native={curNativeLen:F1}  angle={curAngle:F1}°");
    Console.WriteLine($"    PCA axis  : ({evx:F4}, {evy:F4})  angle={pcaAngle:F1}°  (λ ratio={lam1 / (halfTrace - disc + 1e-9):F1}x)");
    Console.WriteLine($"    new join  : ({newJoinX:F1}, {newJoinY:F1})  native={newLen:F1}  angle={newAngle:F1}°{(joinChanged ? "  ← CHANGED" : "")}");
    if (joinChanged || angleChanged)
        Console.WriteLine($"    JSON patch: \"local_join\": {{ \"x\": {newJoinX:F1}, \"y\": {newJoinY:F1} }},  \"native_length\": {newLen:F1},  \"native_angle_deg\": {newAngle:F1}");
    Console.WriteLine();
}

// ── Annotate mode ─────────────────────────────────────────────────────────────

static void AnnotateHead(JsonElement pin, string pinId, string partsDir, string outputDir)
{
    var headFile = pin.GetProperty("head_file").GetString()!;
    var head     = pin.GetProperty("head");
    var center   = ParsePoint(head.GetProperty("local_center"));
    var attach   = ParsePoint(head.GetProperty("local_attach"));
    var radius   = head.GetProperty("local_radius").GetDouble();

    var srcPath = Path.Combine(partsDir, headFile);
    if (!File.Exists(srcPath)) { Console.WriteLine($"    !! missing {srcPath}"); return; }

    using var orig = new Bitmap(srcPath);
    using var bmp  = new Bitmap(orig);
    using var g    = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;

    float imgCx = orig.Width / 2f, imgCy = orig.Height / 2f;

    using (var p = DashedPen(Color.FromArgb(160, Color.Gray), 1f))
    {
        g.DrawLine(p, imgCx, 0, imgCx, orig.Height);
        g.DrawLine(p, 0, imgCy, orig.Width, imgCy);
    }
    using (var p = new Pen(Color.FromArgb(220, Color.DodgerBlue), 2.5f))
        g.DrawEllipse(p,
            (float)(center.X - radius), (float)(center.Y - radius),
            (float)(radius * 2), (float)(radius * 2));

    DrawDot(g, (float)attach.X, (float)attach.Y, 9f, Color.OrangeRed, Color.White);
    DrawDot(g, (float)center.X, (float)center.Y, 9f, Color.LimeGreen, Color.Black);

    DrawLegend(g, new[]
    {
        (Color.LimeGreen,  $"local_center  ({center.X:F1}, {center.Y:F1})"),
        (Color.OrangeRed,  $"local_attach  ({attach.X:F1}, {attach.Y:F1})"),
        (Color.DodgerBlue, $"local_radius  {radius:F0} px"),
        (Color.Gray,       $"image centre  ({imgCx:F0}, {imgCy:F0})"),
    });

    bmp.Save(Path.Combine(outputDir, $"{pinId}_head_debug.png"), ImageFormat.Png);
    Console.WriteLine($"    → {pinId}_head_debug.png");
}

static void AnnotateShaft(JsonElement pin, string pinId, string partsDir, string outputDir, bool litSuffix)
{
    var baseFile  = pin.GetProperty("shaft_file").GetString()!;
    var shaftFile = litSuffix ? baseFile.Replace(".png", "_lit.png") : baseFile;
    var srcPath   = Path.Combine(partsDir, shaftFile);
    if (!File.Exists(srcPath)) return;

    var shaft    = pin.GetProperty("shaft");
    var tip      = ParsePoint(shaft.GetProperty("local_tip"));
    var join     = ParsePoint(shaft.GetProperty("local_join"));

    double dxRaw  = join.X - tip.X, dyRaw = join.Y - tip.Y;
    double axLen  = Math.Sqrt(dxRaw * dxRaw + dyRaw * dyRaw);
    float  axDx   = (float)(dxRaw / axLen), axDy = (float)(dyRaw / axLen);

    var    seg      = shaft.GetProperty("segmentation");
    double tipCap   = seg.GetProperty("tip_cap_length").GetDouble();
    double strStart = seg.GetProperty("stretch_start_distance").GetDouble();
    double strEnd   = seg.GetProperty("stretch_end_distance").GetDouble();
    double native   = shaft.GetProperty("native_length").GetDouble();

    using var orig = new Bitmap(srcPath);
    using var bmp  = new Bitmap(orig);
    using var g    = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;

    float halfW = orig.Width / 2f;

    using (var p = new Pen(Color.FromArgb(140, Color.White), 1.5f))
        g.DrawLine(p, (float)tip.X, (float)tip.Y, (float)join.X, (float)join.Y);

    DrawCapLine(g, tip, axDx, axDy, tipCap,   halfW, Color.FromArgb(200, Color.Cyan));
    DrawCapLine(g, tip, axDx, axDy, strStart,  halfW, Color.FromArgb(200, Color.Magenta));
    DrawCapLine(g, tip, axDx, axDy, strEnd,    halfW, Color.FromArgb(200, Color.Orange));

    DrawDot(g, (float)tip.X,  (float)tip.Y,  8f, Color.Cyan,   Color.Black);
    DrawDot(g, (float)join.X, (float)join.Y, 8f, Color.Yellow, Color.Black);

    var outSuffix = litSuffix ? "_shaft_lit_debug.png" : "_shaft_debug.png";
    DrawLegend(g, new[]
    {
        (Color.Cyan,    $"local_tip   ({tip.X:F1}, {tip.Y:F1})"),
        (Color.Yellow,  $"local_join  ({join.X:F1}, {join.Y:F1})"),
        (Color.Cyan,    $"— tip cap end    dist={tipCap:F0} px"),
        (Color.Magenta, $"— stretch start  dist={strStart:F0} px"),
        (Color.Orange,  $"— stretch end    dist={strEnd:F0} px"),
        (Color.White,   $"  native length  {native:F0} px"),
    });

    bmp.Save(Path.Combine(outputDir, pinId + outSuffix), ImageFormat.Png);
    Console.WriteLine($"    → {pinId}{outSuffix}");
}

// ── Shared drawing helpers ────────────────────────────────────────────────────

static PointF ParsePoint(JsonElement el)
    => new PointF((float)el.GetProperty("x").GetDouble(),
                  (float)el.GetProperty("y").GetDouble());

static void DrawDot(Graphics g, float x, float y, float r, Color fill, Color outline)
{
    using var brush = new SolidBrush(fill);
    using var pen   = new Pen(outline, 1.5f);
    g.FillEllipse(brush, x - r, y - r, r * 2f, r * 2f);
    g.DrawEllipse(pen,   x - r, y - r, r * 2f, r * 2f);
}

static void DrawCapLine(Graphics g, PointF tip, float axDx, float axDy,
                        double dist, float halfLen, Color color)
{
    float px = tip.X + axDx * (float)dist;
    float py = tip.Y + axDy * (float)dist;
    float perpX = -axDy, perpY = axDx;
    using var pen = DashedPen(color, 1.5f);
    g.DrawLine(pen,
        px + perpX * halfLen, py + perpY * halfLen,
        px - perpX * halfLen, py - perpY * halfLen);
}

static Pen DashedPen(Color color, float width)
{
    var pen = new Pen(color, width);
    pen.DashStyle = DashStyle.Dash;
    return pen;
}

static void DrawLegend(Graphics g, (Color color, string text)[] items)
{
    using var font        = new Font("Consolas", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
    using var shadowBrush = new SolidBrush(Color.FromArgb(200, Color.Black));
    float x = 6f, y = 6f;
    foreach (var (col, text) in items)
    {
        var sz = g.MeasureString(text, font);
        g.DrawString(text, font, shadowBrush, x + 1f, y + 1f);
        using var brush = new SolidBrush(col);
        g.DrawString(text, font, brush, x, y);
        y += sz.Height + 1f;
    }
}

// ── Composites mode ──────────────────────────────────────────────────────────

static void RunComposites(JsonElement root, string partsDir, string outputDir, bool useLit)
{
    double[] targetLengths = { 30.0, 50.0, 80.0, 120.0 };
    double[] targetAngles  = { 0.0, 45.0, 90.0, 135.0, 180.0, 225.0, 270.0, 315.0 };
    const double HeadRadiusPx = 14.0;
    const int CellW = 180, CellH = 180, Gap = 6;
    const int MarginLeft = 54, MarginTop = 36;
    int cols   = targetAngles.Length;
    int rows   = targetLengths.Length;
    int gridW  = MarginLeft + cols * (CellW + Gap) + Gap;
    int gridH  = MarginTop  + rows * (CellH + Gap) + Gap;

    Directory.CreateDirectory(outputDir);

    using var titleFont = new Font("Consolas", 12f, FontStyle.Bold,    GraphicsUnit.Pixel);
    using var labelFont = new Font("Consolas",  9f, FontStyle.Regular, GraphicsUnit.Pixel);

    foreach (var pinProp in root.EnumerateObject())
    {
        var pinId = pinProp.Name;
        var pin   = pinProp.Value;
        Console.Write($"  {pinId} ...");

        var shaftBase = pin.GetProperty("shaft_file").GetString()!;
        var shaftFile = useLit ? shaftBase.Replace(".png", "_lit.png") : shaftBase;
        var headFile  = pin.GetProperty("head_file").GetString()!;
        var shaftPath = Path.Combine(partsDir, shaftFile);
        var headPath  = Path.Combine(partsDir, headFile);

        if (!File.Exists(shaftPath) || !File.Exists(headPath))
        {
            Console.WriteLine(" !! missing image(s)");
            continue;
        }

        using var shaftImg = new Bitmap(shaftPath);
        using var headImg  = new Bitmap(headPath);

        using var grid = new Bitmap(gridW, gridH, PixelFormat.Format32bppArgb);
        using var gg   = Graphics.FromImage(grid);
        gg.SmoothingMode     = SmoothingMode.AntiAlias;
        gg.InterpolationMode = InterpolationMode.HighQualityBicubic;
        gg.PixelOffsetMode   = PixelOffsetMode.HighQuality;
        gg.Clear(Color.FromArgb(24, 24, 24));

        using var wBrush  = new SolidBrush(Color.White);
        using var lgBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
        using var dgBrush = new SolidBrush(Color.FromArgb(50, 50, 50));

        // Title
        gg.DrawString($"{pinId}  ({(useLit ? "lit" : "regular")} shafts)", titleFont, wBrush, MarginLeft, 4f);

        // Column headers (angles)
        for (int ai = 0; ai < cols; ai++)
        {
            string lbl = $"{targetAngles[ai]:F0}°";
            float  cx  = MarginLeft + Gap + ai * (CellW + Gap) + CellW / 2f;
            var    sz  = gg.MeasureString(lbl, labelFont);
            gg.DrawString(lbl, labelFont, lgBrush, cx - sz.Width / 2f, 22f);
        }

        // Rows
        for (int li = 0; li < rows; li++)
        {
            double len  = targetLengths[li];
            int    rowY = MarginTop + li * (CellH + Gap) + Gap;

            // Row label (length)
            string rowLbl = $"{len:F0}px";
            var    rowSz  = gg.MeasureString(rowLbl, labelFont);
            gg.DrawString(rowLbl, labelFont, lgBrush,
                           MarginLeft - rowSz.Width - 4f,
                           rowY + CellH / 2f - rowSz.Height / 2f);

            for (int ai = 0; ai < cols; ai++)
            {
                double angle = targetAngles[ai];
                int    cellX = MarginLeft + Gap + ai * (CellW + Gap);
                int    cellY = rowY;

                gg.FillRectangle(dgBrush, cellX, cellY, CellW, CellH);

                Bitmap? composite = null;
                try
                {
                    composite = RenderComposite(pin, shaftImg, headImg, len, angle, HeadRadiusPx);

                    float scaleToFit = Math.Min((CellW - 4f) / composite.Width,
                                               (CellH - 4f) / composite.Height);
                    int dw = scaleToFit < 1f ? (int)(composite.Width  * scaleToFit) : composite.Width;
                    int dh = scaleToFit < 1f ? (int)(composite.Height * scaleToFit) : composite.Height;
                    int ox = cellX + (CellW - dw) / 2;
                    int oy = cellY + (CellH - dh) / 2;
                    gg.DrawImage(composite, ox, oy, dw, dh);
                }
                catch (Exception ex)
                {
                    string msg = ex.Message.Length > 24 ? ex.Message[..24] : ex.Message;
                    using var errBrush = new SolidBrush(Color.OrangeRed);
                    gg.DrawString(msg, labelFont, errBrush, cellX + 2f, cellY + 2f);
                }
                finally { composite?.Dispose(); }
            }
        }

        var outPath = Path.Combine(outputDir, $"{pinId}_composites.png");
        grid.Save(outPath, ImageFormat.Png);
        Console.WriteLine($" → {Path.GetFileName(outPath)}");
    }
}

/// <summary>
/// Renders one composite pin (shaft layers + head) at the given target length and angle.
/// Returns a Bitmap sized to exactly fit the result with transparent background.
/// Cyan dot = tip, yellow dot = join (shaft/head meeting point).
/// </summary>
static Bitmap RenderComposite(JsonElement pin, Bitmap shaftImg, Bitmap headImg,
                               double targetLength, double targetAngleDeg, double headRadiusPx)
{
    var shaft = pin.GetProperty("shaft");
    var head  = pin.GetProperty("head");
    var seg   = shaft.GetProperty("segmentation");

    var    tip          = ParsePoint(shaft.GetProperty("local_tip"));
    var    join         = ParsePoint(shaft.GetProperty("local_join"));
    double nativeLength = shaft.GetProperty("native_length").GetDouble();
    double tipCapLen    = seg.GetProperty("tip_cap_length").GetDouble();
    double headCapLen   = seg.GetProperty("head_cap_length").GetDouble();
    double strStart     = seg.GetProperty("stretch_start_distance").GetDouble();
    double strEnd       = seg.GetProperty("stretch_end_distance").GetDouble();
    double stretchLen   = seg.GetProperty("stretchable_length").GetDouble();
    var    headCenter   = ParsePoint(head.GetProperty("local_center"));
    double headRadius   = head.GetProperty("local_radius").GetDouble();
    double stubDirDeg   = head.GetProperty("stub_direction_deg").GetDouble();

    int shW = shaftImg.Width, shH = shaftImg.Height;
    int hdW = headImg.Width,  hdH = headImg.Height;

    // Target direction (0°=up/−Y, 90°=right/+X)
    double rad = targetAngleDeg * Math.PI / 180.0;
    float  tAx = (float) Math.Sin(rad);
    float  tAy = (float)-Math.Cos(rad);
    float  tNx = -tAy, tNy = tAx;          // left normal

    // Native axis unit vector
    float nDx = join.X - tip.X, nDy = join.Y - tip.Y;
    float nLen = MathF.Sqrt(nDx * nDx + nDy * nDy);
    if (nLen < 0.1f) throw new InvalidOperationException("Degenerate native axis");
    nDx /= nLen; nDy /= nLen;
    float nNx = -nDy, nNy = nDx;

    // Scale factors
    double S           = targetLength / nativeLength;
    double scaledTipC  = tipCapLen  * S;
    double scaledHeadC = headCapLen * S;
    double bodyLen     = targetLength - scaledTipC - scaledHeadC;
    if (bodyLen <= 0)
        throw new InvalidOperationException(
            $"body={bodyLen:F1}px ≤ 0 (targetLength={targetLength:F0}, scale={S:F3})");
    double bodyStretch = bodyLen / stretchLen;
    double headScale   = headRadiusPx > 0 && headRadius > 0
                         ? headRadiusPx / headRadius : S;

    // Build transforms (target tip at origin; shifted after bounds calculation)
    var tipT     = CompLayerTransform(tip, nDx, nDy, nNx, nNy, tAx, tAy, tNx, tNy,
                                      0, 0, S, S, 0.0);
    var bodyT    = CompLayerTransform(tip, nDx, nDy, nNx, nNy, tAx, tAy, tNx, tNy,
                                      0, 0, bodyStretch, S, scaledTipC - strStart * bodyStretch);
    var headCapT = CompLayerTransform(tip, nDx, nDy, nNx, nNy, tAx, tAy, tNx, tNy,
                                      0, 0, S, S, scaledTipC + bodyLen - strEnd * S);

    double nativeCenterAngle = CompNorm360(stubDirDeg + 180.0);
    double headRotDeg        = CompNormSigned(targetAngleDeg - nativeCenterAngle);
    var    targetJoin        = new PointF((float)(targetLength * tAx), (float)(targetLength * tAy));
    var    headT             = CompHeadTransform(headCenter, targetJoin, headRotDeg, headScale);

    // Bounding box of all layers
    var shCorners = new PointF[] { new(0,0), new(shW,0), new(shW,shH), new(0,shH) };
    var hdCorners = new PointF[] { new(0,0), new(hdW,0), new(hdW,hdH), new(0,hdH) };
    var allBounds = CompUnion(CompUnion(CompBounds(shCorners, tipT),     CompBounds(shCorners, bodyT)),
                              CompUnion(CompBounds(shCorners, headCapT), CompBounds(hdCorners, headT)));

    float sx = -(float)allBounds.X, sy = -(float)allBounds.Y;
    tipT    .Translate(sx, sy, MatrixOrder.Append);
    bodyT   .Translate(sx, sy, MatrixOrder.Append);
    headCapT.Translate(sx, sy, MatrixOrder.Append);
    headT   .Translate(sx, sy, MatrixOrder.Append);

    int cW = (int)Math.Ceiling(allBounds.Width)  + 2;
    int cH = (int)Math.Ceiling(allBounds.Height) + 2;

    // Clip bands in source (shaft) pixel space
    const double Seam = 1.5;
    var nb = new RectangleF(0, 0, shW, shH);
    var tipClip     = CompClipBand(nb, tip, nDx, nDy, 0,                        tipCapLen   + Seam);
    var bodyClip    = CompClipBand(nb, tip, nDx, nDy, Math.Max(0, strStart-Seam), strEnd     + Seam);
    var headCapClip = CompClipBand(nb, tip, nDx, nDy, Math.Max(0, strEnd  -Seam), nativeLength);
    var headClip    = new List<PointF> { new(0,0), new(hdW,0), new(hdW,hdH), new(0,hdH) };

    // Render
    var canvas = new Bitmap(cW, cH, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(canvas);
    g.Clear(Color.Transparent);
    g.SmoothingMode     = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode   = PixelOffsetMode.HighQuality;

    CompDrawLayer(g, shaftImg, tipClip,     tipT);
    CompDrawLayer(g, shaftImg, bodyClip,    bodyT);
    CompDrawLayer(g, shaftImg, headCapClip, headCapT);
    CompDrawLayer(g, headImg,  headClip,    headT);

    // Calibration dots
    DrawDot(g, sx, sy, 3.5f, Color.Cyan,   Color.Black);
    DrawDot(g, sx + (float)(targetLength * tAx),
               sy + (float)(targetLength * tAy),
               3.5f, Color.Yellow, Color.Black);

    return canvas;
}

/// <summary>
/// Draws one layer of a composite pin onto <paramref name="g"/>.
/// <paramref name="clipSrc"/> is in source-image pixel space.
/// <paramref name="m"/> maps source pixels → canvas pixels.
/// </summary>
static void CompDrawLayer(Graphics g, Bitmap src, List<PointF> clipSrc, Matrix m)
{
    // 1. Transform clip polygon to device (canvas) space BEFORE changing g.Transform.
    //    With identity world transform, world ≡ device, so SetClip accepts device coords.
    // 2. Set world transform and draw.
    // 3. Restore full state via Save/Restore.
    var state = g.Save();
    try
    {
        if (clipSrc.Count >= 3)
        {
            var devicePts = clipSrc.ToArray();
            m.TransformPoints(devicePts);
            using var cp = new GraphicsPath();
            cp.AddPolygon(devicePts);
            g.ResetTransform();          // ensure world=device when setting clip
            g.SetClip(cp, CombineMode.Replace);
        }
        g.Transform = m;
        g.DrawImage(src, new RectangleF(0, 0, src.Width, src.Height));
    }
    finally { g.Restore(state); }
}

/// <summary>
/// Builds a GDI+ Matrix that maps source pixels to canvas pixels for one shaft layer.
/// Mirrors the CreateLayerTransform math in CompositePinRenderPlanBuilder.
/// GDI+ transform: x' = M.Elements[0]*x + M.Elements[2]*y + M.Elements[4]
///                 y' = M.Elements[1]*x + M.Elements[3]*y + M.Elements[5]
/// </summary>
static Matrix CompLayerTransform(
    PointF nTip,
    float nAx, float nAy,   // native axis unit
    float nNx, float nNy,   // native normal unit
    float tAx, float tAy,   // target axis unit
    float tNx, float tNy,   // target normal unit
    float tTx, float tTy,   // target tip position
    double axialScale, double normalScale, double axialOffset)
{
    double m11 = normalScale * tNx * nNx + axialScale * tAx * nAx;
    double m12 = normalScale * tNx * nNy + axialScale * tAx * nAy;
    double m21 = normalScale * tNy * nNx + axialScale * tAy * nAx;
    double m22 = normalScale * tNy * nNy + axialScale * tAy * nAy;
    double ox  = tTx - (m11 * nTip.X + m12 * nTip.Y) + axialOffset * tAx;
    double oy  = tTy - (m21 * nTip.X + m22 * nTip.Y) + axialOffset * tAy;
    // GDI+ Matrix(a,b,c,d,e,f): x'=a*x+c*y+e, y'=b*x+d*y+f
    // Our formula:               x'=m11*x+m12*y+ox, y'=m21*x+m22*y+oy
    // → a=m11, c=m12, b=m21, d=m22
    return new Matrix((float)m11, (float)m21, (float)m12, (float)m22, (float)ox, (float)oy);
}

/// <summary>
/// Mirrors CreateHeadTransform: scale → rotate → translate anchor to target.
/// Uses MatrixOrder.Append to match WPF's post-multiply (append) behaviour.
/// </summary>
static Matrix CompHeadTransform(PointF anchor, PointF target, double rotDeg, double scale)
{
    var m = new Matrix();
    m.Scale((float)scale, (float)scale, MatrixOrder.Append);
    m.Rotate((float)rotDeg, MatrixOrder.Append);
    var pts = new[] { anchor };
    m.TransformPoints(pts);
    m.Translate(target.X - pts[0].X, target.Y - pts[0].Y, MatrixOrder.Append);
    return m;
}

static List<PointF> CompClipBand(RectangleF b, PointF tip, float ax, float ay,
                                  double minD, double maxD)
{
    var poly = new List<PointF>
    {
        b.Location, new(b.Right, b.Top), new(b.Right, b.Bottom), new(b.Left, b.Bottom)
    };
    poly = CompClipHalf(poly, p => CompAxisDist(tip, ax, ay, p) >= minD, (float)minD, tip, ax, ay);
    poly = CompClipHalf(poly, p => CompAxisDist(tip, ax, ay, p) <= maxD, (float)maxD, tip, ax, ay);
    return poly;
}

static List<PointF> CompClipHalf(List<PointF> poly, Func<PointF, bool> inside,
                                  float boundary, PointF tip, float ax, float ay)
{
    var output  = new List<PointF>();
    if (poly.Count == 0) return output;
    var  prev   = poly[^1];
    bool prevIn = inside(prev);
    foreach (var cur in poly)
    {
        bool curIn = inside(cur);
        if (curIn)
        {
            if (!prevIn) output.Add(CompIntersect(prev, cur, boundary, tip, ax, ay));
            output.Add(cur);
        }
        else if (prevIn)
        {
            output.Add(CompIntersect(prev, cur, boundary, tip, ax, ay));
        }
        prev   = cur;
        prevIn = curIn;
    }
    return output;
}

static PointF CompIntersect(PointF a, PointF b, float boundary, PointF tip, float ax, float ay)
{
    float da = CompAxisDist(tip, ax, ay, a);
    float db = CompAxisDist(tip, ax, ay, b);
    float t  = MathF.Abs(db - da) < 1e-5f ? 0f : (boundary - da) / (db - da);
    return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}

static float CompAxisDist(PointF tip, float ax, float ay, PointF p)
    => (p.X - tip.X) * ax + (p.Y - tip.Y) * ay;

static RectangleF CompBounds(PointF[] corners, Matrix m)
{
    var pts = (PointF[])corners.Clone();
    m.TransformPoints(pts);
    float x0 = pts[0].X, x1 = pts[0].X, y0 = pts[0].Y, y1 = pts[0].Y;
    for (int i = 1; i < pts.Length; i++)
    {
        x0 = Math.Min(x0, pts[i].X); x1 = Math.Max(x1, pts[i].X);
        y0 = Math.Min(y0, pts[i].Y); y1 = Math.Max(y1, pts[i].Y);
    }
    return new RectangleF(x0, y0, x1 - x0, y1 - y0);
}

static RectangleF CompUnion(RectangleF a, RectangleF b)
{
    float x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
    return new RectangleF(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
}

static double CompNorm360(double a)     { while (a < 0) a += 360; while (a >= 360) a -= 360; return a; }
static double CompNormSigned(double a)  { while (a <= -180) a += 360; while (a > 180) a -= 360; return a; }
