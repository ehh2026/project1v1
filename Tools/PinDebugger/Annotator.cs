// Calibration annotation mode (default): draws geometry markers on head/shaft PNG copies.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

internal static class Annotator
{
    internal static void AnnotateHead(JsonElement pin, string pinId, string partsDir, string outputDir)
    {
        var headFile = pin.GetProperty("head_file").GetString()!;
        var head     = pin.GetProperty("head");
        var center   = PinGeometryHelpers.ParsePoint(head.GetProperty("local_center"));
        var attach   = PinGeometryHelpers.ParsePoint(head.GetProperty("local_attach"));
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

        DrawDot(g, center.X, center.Y, 9f, Color.LimeGreen, Color.Black);
        DrawDot(g, attach.X, attach.Y, 9f, Color.OrangeRed, Color.White);

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

    internal static void AnnotateShaft(JsonElement pin, string pinId, string partsDir, string outputDir, bool litSuffix)
    {
        var baseFile  = pin.GetProperty("shaft_file").GetString()!;
        var shaftFile = litSuffix ? baseFile.Replace(".png", "_lit.png") : baseFile;
        var srcPath   = Path.Combine(partsDir, shaftFile);
        if (!File.Exists(srcPath)) return;

        var shaft    = pin.GetProperty("shaft");
        var tip      = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_tip"));
        var join     = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_join"));

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
            g.DrawLine(p, tip.X, tip.Y, join.X, join.Y);

        DrawCapLine(g, tip, axDx, axDy, tipCap,   halfW, Color.FromArgb(200, Color.Cyan));
        DrawCapLine(g, tip, axDx, axDy, strStart,  halfW, Color.FromArgb(200, Color.Magenta));
        DrawCapLine(g, tip, axDx, axDy, strEnd,    halfW, Color.FromArgb(200, Color.Orange));

        DrawDot(g, tip.X,  tip.Y,  8f, Color.Cyan,   Color.Black);
        DrawDot(g, join.X, join.Y, 8f, Color.Yellow, Color.Black);

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

    internal static void DrawDot(Graphics g, float x, float y, float r, Color fill, Color outline)
    {
        using var brush = new SolidBrush(fill);
        using var pen   = new Pen(outline, 1.5f);
        g.FillEllipse(brush, x - r, y - r, r * 2f, r * 2f);
        g.DrawEllipse(pen,   x - r, y - r, r * 2f, r * 2f);
    }

    internal static void DrawCapLine(Graphics g, PointF tip, float axDx, float axDy,
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

    internal static Pen DashedPen(Color color, float width)
    {
        var pen = new Pen(color, width);
        pen.DashStyle = DashStyle.Dash;
        return pen;
    }

    internal static void DrawLegend(Graphics g, (Color color, string text)[] items)
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
}
