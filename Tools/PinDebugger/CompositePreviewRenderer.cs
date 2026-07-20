// Composite grid preview mode (--composites): renders shaft+head at multiple angles/lengths.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

internal static class CompositePreviewRenderer
{
    internal static void RunComposites(JsonElement root, string partsDir, string outputDir, bool useLit)
    {
        double[] targetLengths = { 30.0, 50.0, 80.0, 120.0 };
        double[] targetAngles  = { 0.0, 45.0, 90.0, 135.0, 180.0, 225.0, 270.0, 315.0 };

        double headRadiusPx     = 14.0;
        double shaftHalfWidthPx = 0.0;
        var configPath =
            File.Exists("visual-config.json") ? "visual-config.json"
            : File.Exists("visual-config.default.json") ? "visual-config.default.json"
            : null;
        if (configPath != null)
        {
            using var vcDoc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (vcDoc.RootElement.TryGetProperty("PinParts", out var pp))
            {
                if (pp.TryGetProperty("TargetHeadRadiusPx",     out var hr)) headRadiusPx     = hr.GetDouble();
                if (pp.TryGetProperty("TargetShaftHalfWidthPx", out var sw)) shaftHalfWidthPx = sw.GetDouble();
            }
        }

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

            gg.DrawString($"{pinId}  ({(useLit ? "lit" : "regular")} shafts)", titleFont, wBrush, MarginLeft, 4f);

            for (int ai = 0; ai < cols; ai++)
            {
                string lbl = $"{targetAngles[ai]:F0}°";
                float  cx  = MarginLeft + Gap + ai * (CellW + Gap) + CellW / 2f;
                var    sz  = gg.MeasureString(lbl, labelFont);
                gg.DrawString(lbl, labelFont, lgBrush, cx - sz.Width / 2f, 22f);
            }

            for (int li = 0; li < rows; li++)
            {
                double len  = targetLengths[li];
                int    rowY = MarginTop + li * (CellH + Gap) + Gap;

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
                        composite = RenderComposite(pin, shaftImg, headImg, len, angle, headRadiusPx, shaftHalfWidthPx);

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

    internal static Bitmap RenderComposite(JsonElement pin, Bitmap shaftImg, Bitmap headImg,
                               double targetLength, double targetAngleDeg, double headRadiusPx,
                               double shaftHalfWidthPx = 0.0)
    {
        var shaft = pin.GetProperty("shaft");
        var head  = pin.GetProperty("head");
        var seg   = shaft.GetProperty("segmentation");

        var    tip           = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_tip"));
        var    join          = PinGeometryHelpers.ParsePoint(shaft.GetProperty("local_join"));
        double nativeLength  = shaft.GetProperty("native_length").GetDouble();
        double nativeHalfW   = shaft.TryGetProperty("native_shaft_half_width_px", out var nhw)
                               ? nhw.GetDouble() : 0.0;
        double tipCapLen     = seg.GetProperty("tip_cap_length").GetDouble();
        double headCapLen    = seg.GetProperty("head_cap_length").GetDouble();
        double strStart      = seg.GetProperty("stretch_start_distance").GetDouble();
        double strEnd        = seg.GetProperty("stretch_end_distance").GetDouble();
        double stretchLen    = seg.GetProperty("stretchable_length").GetDouble();
        var    headCenter    = PinGeometryHelpers.ParsePoint(head.GetProperty("local_center"));
        double headRadius    = head.GetProperty("local_radius").GetDouble();
        double stubDirDeg    = head.GetProperty("stub_direction_deg").GetDouble();

        int shW = shaftImg.Width, shH = shaftImg.Height;
        int hdW = headImg.Width,  hdH = headImg.Height;

        double rad = targetAngleDeg * Math.PI / 180.0;
        float  tAx = (float) Math.Sin(rad);
        float  tAy = (float)-Math.Cos(rad);
        float  tNx = -tAy, tNy = tAx;

        float nDx = join.X - tip.X, nDy = join.Y - tip.Y;
        float nLen = MathF.Sqrt(nDx * nDx + nDy * nDy);
        if (nLen < 0.1f) throw new InvalidOperationException("Degenerate native axis");
        nDx /= nLen; nDy /= nLen;
        float nNx = -nDy, nNy = nDx;

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

        double normalScale = (shaftHalfWidthPx > 0.0 && nativeHalfW > 0.0)
                             ? shaftHalfWidthPx / nativeHalfW : S;

        var tipT     = CompLayerTransform(tip, nDx, nDy, nNx, nNy, tAx, tAy, tNx, tNy,
                                          0, 0, S, normalScale, 0.0);
        var bodyT    = CompLayerTransform(tip, nDx, nDy, nNx, nNy, tAx, tAy, tNx, tNy,
                                          0, 0, bodyStretch, normalScale, scaledTipC - strStart * bodyStretch);
        var headCapT = CompLayerTransform(tip, nDx, nDy, nNx, nNy, tAx, tAy, tNx, tNy,
                                          0, 0, S, normalScale, scaledTipC + bodyLen - strEnd * S);

        double nativeCenterAngle = CompNorm360(stubDirDeg + 180.0);
        double headRotDeg        = CompNormSigned(targetAngleDeg - nativeCenterAngle);
        var    targetJoin        = new PointF((float)(targetLength * tAx), (float)(targetLength * tAy));
        var    headT             = CompHeadTransform(headCenter, targetJoin, headRotDeg, headScale);

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

        const double Seam = 1.5;
        var nb = new RectangleF(0, 0, shW, shH);
        var tipClip     = CompClipBand(nb, tip, nDx, nDy, 0,                        tipCapLen   + Seam);
        var bodyClip    = CompClipBand(nb, tip, nDx, nDy, Math.Max(0, strStart-Seam), strEnd     + Seam);
        var headCapClip = CompClipBand(nb, tip, nDx, nDy, Math.Max(0, strEnd  -Seam), nativeLength);
        var headClip    = new List<PointF> { new(0,0), new(hdW,0), new(hdW,hdH), new(0,hdH) };

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

        Annotator.DrawDot(g, sx, sy, 3.5f, Color.Cyan,   Color.Black);
        Annotator.DrawDot(g, sx + (float)(targetLength * tAx),
                             sy + (float)(targetLength * tAy),
                             3.5f, Color.Yellow, Color.Black);

        return canvas;
    }

    private static void CompDrawLayer(Graphics g, Bitmap src, List<PointF> clipSrc, Matrix m)
    {
        var state = g.Save();
        try
        {
            if (clipSrc.Count >= 3)
            {
                var devicePts = clipSrc.ToArray();
                m.TransformPoints(devicePts);
                using var cp = new GraphicsPath();
                cp.AddPolygon(devicePts);
                g.ResetTransform();
                g.SetClip(cp, CombineMode.Replace);
            }
            g.Transform = m;
            g.DrawImage(src, new RectangleF(0, 0, src.Width, src.Height));
        }
        finally { g.Restore(state); }
    }

    private static Matrix CompLayerTransform(
        PointF nTip,
        float nAx, float nAy,
        float nNx, float nNy,
        float tAx, float tAy,
        float tNx, float tNy,
        float tTx, float tTy,
        double axialScale, double normalScale, double axialOffset)
    {
        double m11 = normalScale * tNx * nNx + axialScale * tAx * nAx;
        double m12 = normalScale * tNx * nNy + axialScale * tAx * nAy;
        double m21 = normalScale * tNy * nNx + axialScale * tAy * nAx;
        double m22 = normalScale * tNy * nNy + axialScale * tAy * nAy;
        double ox  = tTx - (m11 * nTip.X + m12 * nTip.Y) + axialOffset * tAx;
        double oy  = tTy - (m21 * nTip.X + m22 * nTip.Y) + axialOffset * tAy;
        return new Matrix((float)m11, (float)m21, (float)m12, (float)m22, (float)ox, (float)oy);
    }

    private static Matrix CompHeadTransform(PointF anchor, PointF target, double rotDeg, double scale)
    {
        var m = new Matrix();
        m.Scale((float)scale, (float)scale, MatrixOrder.Append);
        m.Rotate((float)rotDeg, MatrixOrder.Append);
        var pts = new[] { anchor };
        m.TransformPoints(pts);
        m.Translate(target.X - pts[0].X, target.Y - pts[0].Y, MatrixOrder.Append);
        return m;
    }

    private static List<PointF> CompClipBand(RectangleF b, PointF tip, float ax, float ay,
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

    private static List<PointF> CompClipHalf(List<PointF> poly, Func<PointF, bool> inside,
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

    private static PointF CompIntersect(PointF a, PointF b, float boundary, PointF tip, float ax, float ay)
    {
        float da = CompAxisDist(tip, ax, ay, a);
        float db = CompAxisDist(tip, ax, ay, b);
        float t  = MathF.Abs(db - da) < 1e-5f ? 0f : (boundary - da) / (db - da);
        return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    private static float CompAxisDist(PointF tip, float ax, float ay, PointF p)
        => (p.X - tip.X) * ax + (p.Y - tip.Y) * ay;

    private static RectangleF CompBounds(PointF[] corners, Matrix m)
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

    private static RectangleF CompUnion(RectangleF a, RectangleF b)
    {
        float x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new RectangleF(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    private static double CompNorm360(double a)     { while (a < 0) a += 360; while (a >= 360) a -= 360; return a; }
    private static double CompNormSigned(double a)  { while (a <= -180) a += 360; while (a > 180) a -= 360; return a; }
}
