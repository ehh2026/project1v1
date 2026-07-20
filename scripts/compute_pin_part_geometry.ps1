param(
    [string]$PartsDir = "Images&Content\Assets\Pins_v2\parts",
    [string]$OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$helperSource = @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public sealed class HeadGeometryResult
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; }
}

public sealed class ShaftGeometryResult
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double AxisLength { get; set; }
    public double AxisAngleDeg { get; set; }
}

internal sealed class PixelSample
{
    public double X;
    public double Y;
    public double Weight;
}

public static class PinPartGeometryAnalyzer
{
    public static HeadGeometryResult ComputeHead(string path, byte alphaThreshold)
    {
        using (Bitmap bitmap = new Bitmap(path))
        {
            int width;
            int height;
            byte[] alpha = ReadAlpha(bitmap, out width, out height);

            int opaqueCount = 0;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte a = alpha[(y * width) + x];
                    if (a <= alphaThreshold)
                    {
                        continue;
                    }

                    opaqueCount++;
                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            if (opaqueCount == 0 || maxX < minX || maxY < minY)
            {
                throw new InvalidOperationException("No opaque head pixels were found.");
            }

            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;
            double radius = Math.Max((maxX - minX + 1) / 2.0, (maxY - minY + 1) / 2.0);

            return new HeadGeometryResult
            {
                Width = width,
                Height = height,
                CenterX = centerX,
                CenterY = centerY,
                Radius = radius,
            };
        }
    }

    public static ShaftGeometryResult ComputeShaft(string path, byte primaryThreshold, byte fallbackThreshold)
    {
        using (Bitmap bitmap = new Bitmap(path))
        {
            int width;
            int height;
            byte[] alpha = ReadAlpha(bitmap, out width, out height);

            List<PixelSample> samples = CollectSamples(alpha, width, height, primaryThreshold);
            if (samples.Count < 20)
            {
                samples = CollectSamples(alpha, width, height, fallbackThreshold);
            }

            if (samples.Count < 20)
            {
                throw new InvalidOperationException("No stable shaft pixels were found.");
            }

            double sumWeight = 0.0;
            double sumX = 0.0;
            double sumY = 0.0;

            foreach (PixelSample sample in samples)
            {
                sumWeight += sample.Weight;
                sumX += sample.X * sample.Weight;
                sumY += sample.Y * sample.Weight;
            }

            double centerX = sumX / sumWeight;
            double centerY = sumY / sumWeight;

            double cxx = 0.0;
            double cyy = 0.0;
            double cxy = 0.0;

            foreach (PixelSample sample in samples)
            {
                double dx = sample.X - centerX;
                double dy = sample.Y - centerY;
                cxx += sample.Weight * dx * dx;
                cyy += sample.Weight * dy * dy;
                cxy += sample.Weight * dx * dy;
            }

            double theta = 0.5 * Math.Atan2(2.0 * cxy, cxx - cyy);
            double axisX = Math.Cos(theta);
            double axisY = Math.Sin(theta);
            double perpX = -axisY;
            double perpY = axisX;

            double minProjection = double.PositiveInfinity;
            double maxProjection = double.NegativeInfinity;

            foreach (PixelSample sample in samples)
            {
                double projection = ((sample.X - centerX) * axisX) + ((sample.Y - centerY) * axisY);
                if (projection < minProjection)
                {
                    minProjection = projection;
                }

                if (projection > maxProjection)
                {
                    maxProjection = projection;
                }
            }

            double span = maxProjection - minProjection;
            double band = Math.Max(4.0, Math.Min(18.0, span * 0.03));

            double startWeight = 0.0;
            double startPerp = 0.0;
            double endWeight = 0.0;
            double endPerp = 0.0;

            foreach (PixelSample sample in samples)
            {
                double projection = ((sample.X - centerX) * axisX) + ((sample.Y - centerY) * axisY);
                double perpendicular = ((sample.X - centerX) * perpX) + ((sample.Y - centerY) * perpY);
                if (projection <= minProjection + band)
                {
                    startWeight += sample.Weight;
                    startPerp += perpendicular * sample.Weight;
                }

                if (projection >= maxProjection - band)
                {
                    endWeight += sample.Weight;
                    endPerp += perpendicular * sample.Weight;
                }
            }

            if (startWeight <= 0.0 || endWeight <= 0.0)
            {
                throw new InvalidOperationException("Could not isolate shaft endpoints.");
            }

            startPerp /= startWeight;
            endPerp /= endWeight;

            double startX = centerX + (axisX * minProjection) + (perpX * startPerp);
            double startY = centerY + (axisY * minProjection) + (perpY * startPerp);
            double endX = centerX + (axisX * maxProjection) + (perpX * endPerp);
            double endY = centerY + (axisY * maxProjection) + (perpY * endPerp);

            double dxAxis = endX - startX;
            double dyAxis = endY - startY;
            double axisLength = Math.Sqrt((dxAxis * dxAxis) + (dyAxis * dyAxis));
            double axisAngleDeg = Math.Atan2(dxAxis, -dyAxis) * (180.0 / Math.PI);
            if (axisAngleDeg < 0.0)
            {
                axisAngleDeg += 360.0;
            }

            return new ShaftGeometryResult
            {
                Width = width,
                Height = height,
                StartX = startX,
                StartY = startY,
                EndX = endX,
                EndY = endY,
                AxisLength = axisLength,
                AxisAngleDeg = axisAngleDeg,
            };
        }
    }

    private static List<PixelSample> CollectSamples(byte[] alpha, int width, int height, byte threshold)
    {
        List<PixelSample> samples = new List<PixelSample>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte a = alpha[(y * width) + x];
                if (a < threshold)
                {
                    continue;
                }

                samples.Add(new PixelSample
                {
                    X = x,
                    Y = y,
                    Weight = a / 255.0,
                });
            }
        }

        return samples;
    }

    private static byte[] ReadAlpha(Bitmap bitmap, out int width, out int height)
    {
        width = bitmap.Width;
        height = bitmap.Height;

        using (Bitmap normalized = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(normalized))
            {
                graphics.DrawImage(bitmap, 0, 0, width, height);
            }

            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] raw = new byte[stride * height];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);

                byte[] alpha = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * stride;
                    int alphaOffset = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        alpha[alphaOffset + x] = raw[rowOffset + (x * 4) + 3];
                    }
                }

                return alpha;
            }
            finally
            {
                normalized.UnlockBits(data);
            }
        }
    }
}
"@

Add-Type -TypeDefinition $helperSource -ReferencedAssemblies System.Drawing

function New-RoundedPoint {
    param(
        [double]$X,
        [double]$Y
    )

    [ordered]@{
        x = [Math]::Round($X, 1)
        y = [Math]::Round($Y, 1)
    }
}

function Get-Distance {
    param(
        [hashtable]$PointA,
        [hashtable]$PointB
    )

    $dx = [double]$PointA.x - [double]$PointB.x
    $dy = [double]$PointA.y - [double]$PointB.y
    return [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
}

function ConvertTo-HashtablePoint {
    param([object]$Point)

    New-RoundedPoint -X ([double]$Point.x) -Y ([double]$Point.y)
}

function Get-AngleDegrees {
    param(
        [hashtable]$FromPoint,
        [hashtable]$ToPoint
    )

    $dx = [double]$ToPoint.x - [double]$FromPoint.x
    $dy = [double]$ToPoint.y - [double]$FromPoint.y
    $angle = [Math]::Atan2($dx, -$dy) * 180.0 / [Math]::PI
    if ($angle -lt 0.0) {
        $angle += 360.0
    }

    return [Math]::Round($angle, 1)
}

function New-ShaftSegmentation {
    param([double]$NativeLength)

    $tipCapLength = [Math]::Max(18.0, $NativeLength * 0.10)
    $headCapLength = [Math]::Max(18.0, $NativeLength * 0.12)
    $minimumMiddleLength = $NativeLength * 0.25
    $maximumFixedLength = [Math]::Max(0.0, $NativeLength - $minimumMiddleLength)
    $fixedLength = $tipCapLength + $headCapLength

    if ($fixedLength -gt $maximumFixedLength -and $fixedLength -gt 0.0) {
        $scale = $maximumFixedLength / $fixedLength
        $tipCapLength *= $scale
        $headCapLength *= $scale
    }

    $stretchStartDistance = [Math]::Round($tipCapLength, 1)
    $stretchEndDistance = [Math]::Round([Math]::Max($stretchStartDistance, $NativeLength - $headCapLength), 1)
    $stretchableLength = [Math]::Round([Math]::Max(0.0, $stretchEndDistance - $stretchStartDistance), 1)

    [ordered]@{
        tip_cap_length = [Math]::Round($tipCapLength, 1)
        head_cap_length = [Math]::Round($headCapLength, 1)
        stretch_start_distance = $stretchStartDistance
        stretch_end_distance = $stretchEndDistance
        stretchable_length = $stretchableLength
        minimum_middle_ratio = 0.25
    }
}

$resolvedPartsDir = (Resolve-Path $PartsDir).Path
if (-not $OutputPath) {
    $OutputPath = Join-Path $resolvedPartsDir "pin_part_geometry.json"
}

$manifestPath = Join-Path $resolvedPartsDir "pin_parts_manifest.json"
$manifest = $null
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
}

$headFiles = Get-ChildItem -Path $resolvedPartsDir -Filter "pin_*_head.png" | Sort-Object Name
if (-not $headFiles) {
    throw "No pin head files were found in $resolvedPartsDir"
}

$results = [ordered]@{}

Write-Host "Parts:  $resolvedPartsDir"
Write-Host "Output: $OutputPath"
Write-Host ""

foreach ($headFile in $headFiles) {
    if ($headFile.BaseName -notmatch "^(pin_\d{2})_head$") {
        continue
    }

    $pinId = $Matches[1]
    $shaftPath = Join-Path $resolvedPartsDir "$pinId`_shaft.png"
    if (-not (Test-Path $shaftPath)) {
        Write-Warning "Skipping $pinId because $pinId`_shaft.png is missing."
        continue
    }

    $headResult = [PinPartGeometryAnalyzer]::ComputeHead($headFile.FullName, [byte]10)
    $shaftResult = [PinPartGeometryAnalyzer]::ComputeShaft($shaftPath, [byte]64, [byte]10)

    $head = [ordered]@{
        image_size = [ordered]@{
            w = $headResult.Width
            h = $headResult.Height
        }
        local_center = New-RoundedPoint -X $headResult.CenterX -Y $headResult.CenterY
        local_radius = [Math]::Round($headResult.Radius, 1)
    }

    $shaft = [ordered]@{
        image_size = [ordered]@{
            w = $shaftResult.Width
            h = $shaftResult.Height
        }
        axis_endpoints_local = [ordered]@{
            axis_start = New-RoundedPoint -X $shaftResult.StartX -Y $shaftResult.StartY
            axis_end = New-RoundedPoint -X $shaftResult.EndX -Y $shaftResult.EndY
        }
        axis_length = [Math]::Round($shaftResult.AxisLength, 1)
        axis_angle_deg = [Math]::Round($shaftResult.AxisAngleDeg, 1)
    }

    $alignment = [ordered]@{}
    $manifestEntry = $null
    if ($manifest -and ($manifest.PSObject.Properties.Name -contains $pinId)) {
        $manifestEntry = $manifest.$pinId
    }

    if ($manifestEntry) {
        $headOriginal = New-RoundedPoint `
            -X ($head.local_center.x + [double]$manifestEntry.head_crop_box.x0) `
            -Y ($head.local_center.y + [double]$manifestEntry.head_crop_box.y0)
        $head.original_center = $headOriginal
        $alignment.head_center_delta_px = [Math]::Round(
            (Get-Distance -PointA $headOriginal -PointB (ConvertTo-HashtablePoint $manifestEntry.head_center)),
            2
        )

        $startOriginal = New-RoundedPoint `
            -X ($shaft.axis_endpoints_local.axis_start.x + [double]$manifestEntry.shaft_crop_offset.x) `
            -Y ($shaft.axis_endpoints_local.axis_start.y + [double]$manifestEntry.shaft_crop_offset.y)
        $endOriginal = New-RoundedPoint `
            -X ($shaft.axis_endpoints_local.axis_end.x + [double]$manifestEntry.shaft_crop_offset.x) `
            -Y ($shaft.axis_endpoints_local.axis_end.y + [double]$manifestEntry.shaft_crop_offset.y)

        $shaft.axis_endpoints_original = [ordered]@{
            axis_start = $startOriginal
            axis_end = $endOriginal
        }

        $manifestTip = ConvertTo-HashtablePoint $manifestEntry.tip
        $manifestHeadCenter = ConvertTo-HashtablePoint $manifestEntry.head_center

        $directCost =
            (Get-Distance -PointA $startOriginal -PointB $manifestTip) +
            (Get-Distance -PointA $endOriginal -PointB $manifestHeadCenter)
        $swappedCost =
            (Get-Distance -PointA $endOriginal -PointB $manifestTip) +
            (Get-Distance -PointA $startOriginal -PointB $manifestHeadCenter)

        if ($directCost -le $swappedCost) {
            $tipLocal = $shaft.axis_endpoints_local.axis_start
            $headLocal = $shaft.axis_endpoints_local.axis_end
            $tipOriginal = $startOriginal
            $headOriginalEndpoint = $endOriginal
        }
        else {
            $tipLocal = $shaft.axis_endpoints_local.axis_end
            $headLocal = $shaft.axis_endpoints_local.axis_start
            $tipOriginal = $endOriginal
            $headOriginalEndpoint = $startOriginal
        }

        $shaft.labeled_endpoints_local = [ordered]@{
            tip = $tipLocal
            head_side = $headLocal
            join = $headLocal
        }
        $shaft.labeled_endpoints_original = [ordered]@{
            tip = $tipOriginal
            head_side = $headOriginalEndpoint
            join = $headOriginalEndpoint
        }

        $nativeLength = [Math]::Round((Get-Distance -PointA $tipLocal -PointB $headLocal), 1)
        $nativeAngle = Get-AngleDegrees -FromPoint $tipLocal -ToPoint $headLocal
        $shaft.tip_to_head_side_angle_deg = $nativeAngle
        $shaft.tip_to_head_side_length = $nativeLength
        $shaft.native_angle_deg = $nativeAngle
        $shaft.native_length = $nativeLength
        $shaft.local_tip = $tipLocal
        $shaft.local_join = $headLocal
        $shaft.original_tip = $tipOriginal
        $shaft.original_join = $headOriginalEndpoint
        $shaft.segmentation = New-ShaftSegmentation -NativeLength $nativeLength

        $headAttachLocal = New-RoundedPoint `
            -X ([double]$headOriginalEndpoint.x - [double]$manifestEntry.head_crop_box.x0) `
            -Y ([double]$headOriginalEndpoint.y - [double]$manifestEntry.head_crop_box.y0)
        $head.local_attach = $headAttachLocal
        $head.original_attach = $headOriginalEndpoint
        $head.stub_direction_deg = Get-AngleDegrees -FromPoint $head.local_center -ToPoint $headAttachLocal
        $head.center_to_attach_distance = [Math]::Round(
            (Get-Distance -PointA $head.local_center -PointB $headAttachLocal),
            1
        )

        $alignment.tip_delta_px = [Math]::Round(
            (Get-Distance -PointA $tipOriginal -PointB $manifestTip),
            2
        )
        $alignment.head_side_vs_center_delta_px = [Math]::Round(
            (Get-Distance -PointA $headOriginalEndpoint -PointB $manifestHeadCenter),
            2
        )
        $alignment.head_attach_inside_crop = (
            ([double]$headAttachLocal.x -ge 0.0) -and
            ([double]$headAttachLocal.y -ge 0.0) -and
            ([double]$headAttachLocal.x -le [double]$head.image_size.w) -and
            ([double]$headAttachLocal.y -le [double]$head.image_size.h)
        )
    }

    $entry = [ordered]@{
        head_file = $headFile.Name
        shaft_file = [IO.Path]::GetFileName($shaftPath)
        head = $head
        shaft = $shaft
    }

    if ($alignment.Count -gt 0) {
        $entry.alignment = $alignment
    }

    $results[$pinId] = $entry
    Write-Host (
        "{0}: head=({1},{2}) shaft tip=({3},{4}) shaft head=({5},{6})" -f
        $pinId,
        $head.local_center.x,
        $head.local_center.y,
        $shaft.labeled_endpoints_local.tip.x,
        $shaft.labeled_endpoints_local.tip.y,
        $shaft.labeled_endpoints_local.head_side.x,
        $shaft.labeled_endpoints_local.head_side.y
    )
}

$results | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host ""
Write-Host "Saved: $OutputPath"
