// Pin geometry debug annotator + shadow cleaner.
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

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;

bool cleanMode = args.Any(a => a == "--clean");
var  posArgs   = args.Where(a => a != "--clean").ToArray();

var partsDir  = posArgs.Length > 0 ? posArgs[0] : Path.Combine("Images&Content", "Pins_v2", "parts");
var outputDir = posArgs.Length > 1 ? posArgs[1]
    : cleanMode
        ? Path.Combine("Tools", "PinDebugger", "cleaned")
        : Path.Combine("Tools", "PinDebugger", "output");

Directory.CreateDirectory(outputDir);
Console.WriteLine($"Mode   : {(cleanMode ? "clean (remove disconnected shadow islands)" : "annotate")}");
Console.WriteLine($"Parts  : {partsDir}");
Console.WriteLine($"Output : {outputDir}");
Console.WriteLine();

var raw  = File.ReadAllBytes(Path.Combine(partsDir, "pin_part_geometry.json"));
int bom  = (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) ? 3 : 0;
var json = Encoding.UTF8.GetString(raw, bom, raw.Length - bom);
using var doc = JsonDocument.Parse(json);

int count = 0;
foreach (var pinProp in doc.RootElement.EnumerateObject())
{
    var pinId = pinProp.Name;
    var pin   = pinProp.Value;
    Console.WriteLine($"  {pinId}");

    if (cleanMode)
    {
        CleanShaft(pin, pinId, partsDir, outputDir, litSuffix: false);
        CleanShaft(pin, pinId, partsDir, outputDir, litSuffix: true);
    }
    else
    {
        AnnotateHead (pin, pinId, partsDir, outputDir);
        AnnotateShaft(pin, pinId, partsDir, outputDir, litSuffix: false);
        AnnotateShaft(pin, pinId, partsDir, outputDir, litSuffix: true);
    }
    count++;
}

Console.WriteLine();
Console.WriteLine($"Done — {count} pins processed.");

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
