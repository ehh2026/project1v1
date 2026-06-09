param(
    [string]$ConfigPath = "visual-config.json",
    [string]$ExcelPath = "Coordinates for map.xlsx",
    [string]$MapImagePath = "Images&Content\World Map Extra Large.jpg",
    [string]$OutputPath = "Images&Content\manual-layouts.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem

$helperSource = @"
using System;
using System.Collections.Generic;

public sealed class SeedPoint
{
    public double X;
    public double Y;

    public SeedPoint() { }

    public SeedPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}

public sealed class SeedLocation
{
    public string Id = "";
    public string Name = "";
    public double PixelX;
    public double PixelY;
}

public sealed class SeedCluster
{
    public string Id = "";
    public List<SeedLocation> Locations = new List<SeedLocation>();
    public SeedPoint CenterPoint = new SeedPoint();
}

public sealed class SeedViewport
{
    public double SourceImageWidth;
    public double SourceImageHeight;
    public double ViewportX;
    public double ViewportY;
    public double ViewportWidth;
    public double ViewportHeight;
    public double ZoomLevel;

    // Mirror ViewportState.GetSourceRect() so SourceToScreen matches the app exactly.
    // CroppedBitmap uses integer pixel boundaries; MapImage.Stretch=Fill scales that crop
    // to fill the container, so scale must be derived from the crop rect, not the virtual viewport.
    public System.Drawing.Rectangle GetSourceRect()
    {
        int x = Math.Max(0, (int)Math.Floor(ViewportX));
        int y = Math.Max(0, (int)Math.Floor(ViewportY));
        int w = (int)Math.Min(Math.Ceiling(ViewportWidth),  SourceImageWidth  - x);
        int h = (int)Math.Min(Math.Ceiling(ViewportHeight), SourceImageHeight - y);
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        return new System.Drawing.Rectangle(x, y, w, h);
    }

    public SeedPoint SourceToScreen(double sourceX, double sourceY, double containerWidth, double containerHeight)
    {
        var rect = GetSourceRect();
        double scaleX = containerWidth  / rect.Width;
        double scaleY = containerHeight / rect.Height;
        return new SeedPoint((sourceX - rect.X) * scaleX, (sourceY - rect.Y) * scaleY);
    }

    public SeedPoint ScreenToSource(double screenX, double screenY, double containerWidth, double containerHeight)
    {
        var rect = GetSourceRect();
        double scaleX = rect.Width  / containerWidth;
        double scaleY = rect.Height / containerHeight;
        return new SeedPoint(screenX * scaleX + rect.X, screenY * scaleY + rect.Y);
    }

    public static SeedViewport CreateFullMapView(double sourceWidth, double sourceHeight, double containerWidth, double containerHeight)
    {
        double sourceAspect = sourceWidth / sourceHeight;
        double containerAspect = containerWidth / containerHeight;
        double viewportWidth;
        double viewportHeight;

        if (sourceAspect > containerAspect)
        {
            viewportWidth = sourceWidth;
            viewportHeight = sourceWidth / containerAspect;
        }
        else
        {
            viewportHeight = sourceHeight;
            viewportWidth = sourceHeight * containerAspect;
        }

        SeedViewport viewport = new SeedViewport();
        viewport.SourceImageWidth = sourceWidth;
        viewport.SourceImageHeight = sourceHeight;
        viewport.ViewportWidth = viewportWidth;
        viewport.ViewportHeight = viewportHeight;
        viewport.ViewportX = (sourceWidth - viewportWidth) / 2.0;
        viewport.ViewportY = (sourceHeight - viewportHeight) / 2.0;
        viewport.ZoomLevel = 1.0;
        return viewport;
    }

    public static SeedViewport CreateZoomedView(double centerX, double centerY, double zoomLevel, double sourceWidth, double sourceHeight, double containerWidth, double containerHeight)
    {
        SeedViewport full = CreateFullMapView(sourceWidth, sourceHeight, containerWidth, containerHeight);
        SeedViewport state = new SeedViewport();
        state.SourceImageWidth = sourceWidth;
        state.SourceImageHeight = sourceHeight;
        state.ViewportWidth = full.ViewportWidth / zoomLevel;
        state.ViewportHeight = full.ViewportHeight / zoomLevel;
        state.ViewportX = centerX - (state.ViewportWidth / 2.0);
        state.ViewportY = centerY - (state.ViewportHeight / 2.0);
        state.ZoomLevel = zoomLevel;
        ClampViewport(state);
        return state;
    }

    private static void ClampViewport(SeedViewport state)
    {
        if (state.ViewportX < 0) state.ViewportX = 0;
        if (state.ViewportY < 0) state.ViewportY = 0;
        if (state.ViewportX + state.ViewportWidth > state.SourceImageWidth)
            state.ViewportX = state.SourceImageWidth - state.ViewportWidth;
        if (state.ViewportY + state.ViewportHeight > state.SourceImageHeight)
            state.ViewportY = state.SourceImageHeight - state.ViewportHeight;
        if (state.ViewportX < 0)
        {
            state.ViewportWidth += state.ViewportX;
            state.ViewportX = 0;
        }
        if (state.ViewportY < 0)
        {
            state.ViewportHeight += state.ViewportY;
            state.ViewportY = 0;
        }
    }
}

public sealed class SeedRadialConfig
{
    public int MinLocationsForExtension;
    public double ProximityThresholdPixels;
    public double ExtensionLineLength;
    public double AngleNudgeThreshold;
    public double AngleNudgeAmount;
    public double MinimumLineLength;
}

public sealed class SeedExtension
{
    public SeedLocation Location = null;
    public SeedPoint OriginalPosition = new SeedPoint();
    public SeedPoint ExtendedPosition = new SeedPoint();
    public double Angle;
}

public sealed class AnglePlacement
{
    public SeedLocation Location = null;
    public SeedPoint ScreenPosition = new SeedPoint();
    public double NaturalAngle;
}

public static class SeedLayoutMath
{
    public static List<SeedCluster> ClusterLocations(List<SeedLocation> locations, double threshold)
    {
        List<SeedCluster> clusters = new List<SeedCluster>();
        HashSet<string> processed = new HashSet<string>();
        int clusterIndex = 0;

        foreach (SeedLocation location in locations)
        {
            if (processed.Contains(location.Id))
                continue;

            List<SeedLocation> clusterLocations = FindNearbyLocations(location, locations, processed, threshold);
            SeedCluster cluster = new SeedCluster();
            cluster.Id = "cluster_" + clusterIndex.ToString("D3");
            cluster.Locations = clusterLocations;
            cluster.CenterPoint = CalculateCenterPoint(clusterLocations);
            clusters.Add(cluster);

            foreach (SeedLocation item in clusterLocations)
                processed.Add(item.Id);

            clusterIndex++;
        }

        return clusters;
    }

    private static List<SeedLocation> FindNearbyLocations(SeedLocation seed, List<SeedLocation> allLocations, HashSet<string> processed, double threshold)
    {
        List<SeedLocation> cluster = new List<SeedLocation>();
        cluster.Add(seed);
        Queue<SeedLocation> queue = new Queue<SeedLocation>();
        queue.Enqueue(seed);
        HashSet<string> inCluster = new HashSet<string>();
        inCluster.Add(seed.Id);

        while (queue.Count > 0)
        {
            SeedLocation current = queue.Dequeue();
            foreach (SeedLocation other in allLocations)
            {
                if (processed.Contains(other.Id) || inCluster.Contains(other.Id))
                    continue;

                if (CalculateDistance(current.PixelX, current.PixelY, other.PixelX, other.PixelY) <= threshold)
                {
                    cluster.Add(other);
                    inCluster.Add(other.Id);
                    queue.Enqueue(other);
                }
            }
        }

        return cluster;
    }

    // Mirror RadialExtensionCalculator.DetectDenseGroups which receives already-projected
    // screen-space positions and measures ProximityThresholdPixels in screen space.
    // Passing source-pixel coordinates produced different (much larger) groups.
    public static List<SeedCluster> DetectDenseGroups(
        List<SeedLocation> locations,
        Dictionary<string, SeedPoint> screenPositions,
        int minLocations,
        double threshold)
    {
        List<SeedCluster> groups = new List<SeedCluster>();
        HashSet<string> processed = new HashSet<string>();

        foreach (SeedLocation location in locations)
        {
            if (processed.Contains(location.Id) || !screenPositions.ContainsKey(location.Id))
                continue;

            List<SeedLocation> cluster = new List<SeedLocation>();
            cluster.Add(location);
            Queue<SeedLocation> queue = new Queue<SeedLocation>();
            queue.Enqueue(location);
            HashSet<string> inCluster = new HashSet<string>();
            inCluster.Add(location.Id);

            while (queue.Count > 0)
            {
                SeedLocation current = queue.Dequeue();
                SeedPoint currentScreen = screenPositions[current.Id];

                foreach (SeedLocation other in locations)
                {
                    if (processed.Contains(other.Id) || inCluster.Contains(other.Id))
                        continue;
                    if (!screenPositions.ContainsKey(other.Id))
                        continue;

                    SeedPoint otherScreen = screenPositions[other.Id];
                    if (CalculateDistance(currentScreen.X, currentScreen.Y, otherScreen.X, otherScreen.Y) <= threshold)
                    {
                        cluster.Add(other);
                        inCluster.Add(other.Id);
                        queue.Enqueue(other);
                    }
                }
            }

            if (cluster.Count >= minLocations)
            {
                SeedCluster group = new SeedCluster();
                group.Locations = cluster;
                group.CenterPoint = CalculateCenterPoint(cluster);
                groups.Add(group);

                foreach (SeedLocation item in cluster)
                    processed.Add(item.Id);
            }
        }

        return groups;
    }

    public static List<SeedExtension> CalculateRadialExtensions(SeedCluster group, Dictionary<string, SeedPoint> screenPositions, double canvasWidth, double canvasHeight, SeedRadialConfig config)
    {
        List<SeedExtension> extensions = new List<SeedExtension>();
        if (group == null || group.Locations.Count == 0)
            return extensions;

        SeedPoint screenCenter = CalculateCenterPointFromScreen(group.Locations, screenPositions);
        List<AnglePlacement> placements = new List<AnglePlacement>();

        foreach (SeedLocation location in group.Locations)
        {
            if (!screenPositions.ContainsKey(location.Id))
                continue;

            SeedPoint screenPosition = screenPositions[location.Id];
            double dx = screenPosition.X - screenCenter.X;
            double dy = screenPosition.Y - screenCenter.Y;
            double angleRadians = Math.Atan2(dx, -dy);
            double angleDegrees = angleRadians * (180.0 / Math.PI);
            if (angleDegrees < 0) angleDegrees += 360.0;

            AnglePlacement placement = new AnglePlacement();
            placement.Location = location;
            placement.ScreenPosition = screenPosition;
            placement.NaturalAngle = angleDegrees;
            placements.Add(placement);
        }

        placements.Sort(delegate(AnglePlacement a, AnglePlacement b) { return a.NaturalAngle.CompareTo(b.NaturalAngle); });
        NudgeAnglesApart(placements, config);
        PreventConvergingLines(placements, config);
        PreventLineIntersections(placements, config);

        foreach (AnglePlacement placement in placements)
        {
            double angleRadians = placement.NaturalAngle * (Math.PI / 180.0);
            double length = config.ExtensionLineLength;
            double extendedX = placement.ScreenPosition.X + length * Math.Sin(angleRadians);
            double extendedY = placement.ScreenPosition.Y - length * Math.Cos(angleRadians);

            if (extendedX < 0 || extendedX > canvasWidth || extendedY < 0 || extendedY > canvasHeight)
            {
                double adjustedLength = CalculateMaxLength(placement.ScreenPosition, angleRadians, canvasWidth, canvasHeight, length, config.MinimumLineLength);
                extendedX = placement.ScreenPosition.X + adjustedLength * Math.Sin(angleRadians);
                extendedY = placement.ScreenPosition.Y - adjustedLength * Math.Cos(angleRadians);
            }

            SeedExtension extension = new SeedExtension();
            extension.Location = placement.Location;
            extension.OriginalPosition = new SeedPoint(placement.ScreenPosition.X, placement.ScreenPosition.Y);
            extension.ExtendedPosition = new SeedPoint(extendedX, extendedY);
            extension.Angle = placement.NaturalAngle;
            extensions.Add(extension);
        }

        return extensions;
    }

    public static string GenerateLayoutKey(List<SeedLocation> locations, SeedViewport viewport, SeedRadialConfig config)
    {
        List<string> names = new List<string>();
        foreach (SeedLocation location in locations)
            names.Add(location.Name);
        names.Sort(StringComparer.Ordinal);
        string locationHash = ComputeHash(string.Join("|", names.ToArray()));

        double centerX = viewport.ViewportX + (viewport.ViewportWidth / 2.0);
        double centerY = viewport.ViewportY + (viewport.ViewportHeight / 2.0);

        List<string> parts = new List<string>();
        parts.Add(locationHash);
        parts.Add("z" + viewport.ZoomLevel.ToString("F2"));
        parts.Add("c" + centerX.ToString("F2") + "_" + centerY.ToString("F2"));
        parts.Add("s" + viewport.ViewportWidth.ToString("F0") + "x" + viewport.ViewportHeight.ToString("F0"));
        parts.Add("m" + config.MinLocationsForExtension.ToString());
        parts.Add("p" + config.ProximityThresholdPixels.ToString("F1"));
        parts.Add("l" + config.ExtensionLineLength.ToString("F1"));
        parts.Add("n" + config.MinimumLineLength.ToString("F1"));
        return string.Join("_", parts.ToArray());
    }

    private static string ComputeHash(string input)
    {
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(bytes);
            string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return hex.Substring(0, 16);
        }
    }

    private static SeedPoint CalculateCenterPoint(List<SeedLocation> locations)
    {
        double sumX = 0;
        double sumY = 0;
        foreach (SeedLocation location in locations)
        {
            sumX += location.PixelX;
            sumY += location.PixelY;
        }

        return new SeedPoint(sumX / locations.Count, sumY / locations.Count);
    }

    private static SeedPoint CalculateCenterPointFromScreen(List<SeedLocation> locations, Dictionary<string, SeedPoint> screenPositions)
    {
        double sumX = 0;
        double sumY = 0;
        int count = 0;
        foreach (SeedLocation location in locations)
        {
            if (!screenPositions.ContainsKey(location.Id))
                continue;
            sumX += screenPositions[location.Id].X;
            sumY += screenPositions[location.Id].Y;
            count++;
        }

        if (count == 0) return new SeedPoint();
        return new SeedPoint(sumX / count, sumY / count);
    }

    private static void NudgeAnglesApart(List<AnglePlacement> placements, SeedRadialConfig config)
    {
        if (placements.Count < 2)
            return;

        int maxIterations = 10;
        double maxAngleRangeToCheck = 45.0;
        int iteration = 0;
        bool needsAdjustment = true;

        while (needsAdjustment && iteration < maxIterations)
        {
            needsAdjustment = false;
            iteration++;

            for (int i = 0; i < placements.Count; i++)
            {
                for (int j = i + 1; j < placements.Count; j++)
                {
                    double angleDiff = (placements[j].NaturalAngle - placements[i].NaturalAngle + 360.0) % 360.0;
                    if (angleDiff > maxAngleRangeToCheck)
                        break;

                    if (angleDiff < config.AngleNudgeThreshold && angleDiff > 0.01)
                    {
                        needsAdjustment = true;
                        double nudge = config.AngleNudgeAmount / 2.0;
                        placements[i].NaturalAngle -= nudge;
                        placements[j].NaturalAngle += nudge;
                    }
                }
            }

            if (needsAdjustment)
                placements.Sort(delegate(AnglePlacement a, AnglePlacement b) { return a.NaturalAngle.CompareTo(b.NaturalAngle); });
        }
    }

    private static void PreventConvergingLines(List<AnglePlacement> placements, SeedRadialConfig config)
    {
        if (placements.Count < 2)
            return;

        int maxIterations = 5;
        double maxAngleRangeToCheck = 90.0;
        int iteration = 0;
        bool needsAdjustment = true;

        while (needsAdjustment && iteration < maxIterations)
        {
            needsAdjustment = false;
            iteration++;

            for (int i = 0; i < placements.Count; i++)
            {
                for (int j = i + 1; j < placements.Count; j++)
                {
                    double angleDiff = (placements[j].NaturalAngle - placements[i].NaturalAngle + 360.0) % 360.0;
                    if (angleDiff > maxAngleRangeToCheck)
                        break;

                    double distanceAtOrigin = CalculateDistance(
                        placements[i].ScreenPosition.X, placements[i].ScreenPosition.Y,
                        placements[j].ScreenPosition.X, placements[j].ScreenPosition.Y);

                    double angle1 = placements[i].NaturalAngle * (Math.PI / 180.0);
                    double angle2 = placements[j].NaturalAngle * (Math.PI / 180.0);
                    SeedPoint end1 = new SeedPoint(
                        placements[i].ScreenPosition.X + config.ExtensionLineLength * Math.Sin(angle1),
                        placements[i].ScreenPosition.Y - config.ExtensionLineLength * Math.Cos(angle1));
                    SeedPoint end2 = new SeedPoint(
                        placements[j].ScreenPosition.X + config.ExtensionLineLength * Math.Sin(angle2),
                        placements[j].ScreenPosition.Y - config.ExtensionLineLength * Math.Cos(angle2));

                    double distanceAtExtension = CalculateDistance(end1.X, end1.Y, end2.X, end2.Y);
                    if (distanceAtExtension < distanceAtOrigin * 0.95)
                    {
                        needsAdjustment = true;
                        placements[i].NaturalAngle -= config.AngleNudgeAmount;
                        placements[j].NaturalAngle += config.AngleNudgeAmount;
                    }
                }
            }

            if (needsAdjustment)
                placements.Sort(delegate(AnglePlacement a, AnglePlacement b) { return a.NaturalAngle.CompareTo(b.NaturalAngle); });
        }
    }

    private static void PreventLineIntersections(List<AnglePlacement> placements, SeedRadialConfig config)
    {
        if (placements.Count < 2)
            return;

        int maxIterations = 10;
        int iteration = 0;
        bool foundIntersection = true;

        while (foundIntersection && iteration < maxIterations)
        {
            foundIntersection = false;
            iteration++;

            for (int i = 0; i < placements.Count; i++)
            {
                double angle1 = placements[i].NaturalAngle * (Math.PI / 180.0);
                SeedPoint line1End = new SeedPoint(
                    placements[i].ScreenPosition.X + config.ExtensionLineLength * Math.Sin(angle1),
                    placements[i].ScreenPosition.Y - config.ExtensionLineLength * Math.Cos(angle1));

                for (int j = i + 1; j < placements.Count; j++)
                {
                    double angle2 = placements[j].NaturalAngle * (Math.PI / 180.0);
                    SeedPoint line2End = new SeedPoint(
                        placements[j].ScreenPosition.X + config.ExtensionLineLength * Math.Sin(angle2),
                        placements[j].ScreenPosition.Y - config.ExtensionLineLength * Math.Cos(angle2));

                    if (DoLinesIntersect(placements[i].ScreenPosition, line1End, placements[j].ScreenPosition, line2End))
                    {
                        foundIntersection = true;
                        placements[i].NaturalAngle -= config.AngleNudgeAmount * 2.0;
                        placements[j].NaturalAngle += config.AngleNudgeAmount * 2.0;
                    }
                }
            }

            if (foundIntersection)
                placements.Sort(delegate(AnglePlacement a, AnglePlacement b) { return a.NaturalAngle.CompareTo(b.NaturalAngle); });
        }
    }

    private static bool DoLinesIntersect(SeedPoint p1, SeedPoint p2, SeedPoint p3, SeedPoint p4)
    {
        double d1x = p2.X - p1.X;
        double d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X;
        double d2y = p4.Y - p3.Y;
        double denominator = d1x * d2y - d1y * d2x;
        if (Math.Abs(denominator) < 0.0001)
            return false;

        double t1 = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denominator;
        double t2 = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / denominator;
        return t1 > 0.01 && t1 < 0.99 && t2 > 0.01 && t2 < 0.99;
    }

    private static double CalculateMaxLength(SeedPoint center, double angleRadians, double canvasWidth, double canvasHeight, double defaultLength, double minimumLength)
    {
        double sinAngle = Math.Sin(angleRadians);
        double cosAngle = Math.Cos(angleRadians);
        double maxLength = defaultLength;

        if (sinAngle > 0)
            maxLength = Math.Min(maxLength, (canvasWidth - center.X) / sinAngle);
        else if (sinAngle < 0)
            maxLength = Math.Min(maxLength, center.X / -sinAngle);

        if (cosAngle > 0)
            maxLength = Math.Min(maxLength, center.Y / cosAngle);
        else if (cosAngle < 0)
            maxLength = Math.Min(maxLength, (canvasHeight - center.Y) / -cosAngle);

        return Math.Max(minimumLength, maxLength * 0.9);
    }

    private static double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2;
        double dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
"@

Add-Type -TypeDefinition $helperSource

function Get-SharedStrings {
    param([System.IO.Compression.ZipArchive]$Zip)

    $sharedStrings = @{}
    $entry = $Zip.GetEntry("xl/sharedStrings.xml")
    if (-not $entry) {
        return $sharedStrings
    }

    $stream = $entry.Open()
    try {
        $reader = New-Object System.IO.StreamReader($stream)
        try {
            [xml]$xml = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $index = 0
        foreach ($si in $xml.sst.si) {
            if ($si.t) {
                $sharedStrings[$index] = [string]$si.t
            }
            elseif ($si.r) {
                $sharedStrings[$index] = (($si.r | ForEach-Object { $_.t }) -join "")
            }
            $index++
        }
    }
    finally {
        $stream.Dispose()
    }

    return $sharedStrings
}

function Get-CellValue {
    param(
        [System.Xml.XmlElement]$Cell,
        [hashtable]$SharedStrings
    )

    $cellType = $Cell.GetAttribute("t")
    if ($cellType -eq "inlineStr") {
        $inlineNode = $Cell.SelectSingleNode("./*[local-name()='is']/*[local-name()='t']")
        if ($inlineNode) {
            return [string]$inlineNode.InnerText
        }
        return ""
    }

    $valueNode = $Cell.SelectSingleNode("./*[local-name()='v']")
    if (-not $valueNode) {
        return ""
    }

    $value = [string]$valueNode.InnerText
    if ($cellType -eq "s") {
        $index = 0
        if ([int]::TryParse($value, [ref]$index) -and $SharedStrings.ContainsKey($index)) {
            return [string]$SharedStrings[$index]
        }
    }

    return $value
}

function Get-ExcelLocations {
    param([string]$Path)

    $locations = New-Object 'System.Collections.Generic.List[SeedLocation]'
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $sharedStrings = Get-SharedStrings -Zip $zip
        $entry = $zip.GetEntry("xl/worksheets/sheet1.xml")
        if (-not $entry) {
            throw "sheet1.xml not found in $Path"
        }

        $stream = $entry.Open()
        try {
            $reader = New-Object System.IO.StreamReader($stream)
            try {
                [xml]$xml = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }

        $rowIndex = 0
        foreach ($row in $xml.worksheet.sheetData.row) {
            $rowIndex++
            if ($rowIndex -eq 1) {
                continue
            }

            $cellValues = @{}
            foreach ($cell in $row.c) {
                $cellRef = [string]$cell.r
                if (-not $cellRef) {
                    continue
                }

                $column = -join ($cellRef.ToCharArray() | Where-Object { [char]::IsLetter($_) })
                $cellValues[$column] = Get-CellValue -Cell $cell -SharedStrings $sharedStrings
            }

            if (-not $cellValues.ContainsKey("A") -or -not $cellValues.ContainsKey("E") -or -not $cellValues.ContainsKey("F")) {
                continue
            }

            $pixelX = 0.0
            $pixelY = 0.0
            if (-not [double]::TryParse([string]$cellValues["E"], [ref]$pixelX)) {
                continue
            }
            if (-not [double]::TryParse([string]$cellValues["F"], [ref]$pixelY)) {
                continue
            }

            $location = New-Object SeedLocation
            $location.Id = ("loc_{0:D3}" -f $rowIndex)
            $location.Name = [string]$cellValues["A"]
            $location.PixelX = $pixelX
            $location.PixelY = $pixelY
            $locations.Add($location)
        }
    }
    finally {
        $zip.Dispose()
    }

    return $locations
}

function New-LayoutMarker {
    param(
        [SeedLocation]$Location,
        [SeedPoint]$Original,
        [SeedPoint]$Extended,
        [double]$Angle
    )

    $dx = $Extended.X - $Original.X
    $dy = $Extended.Y - $Original.Y

    return [ordered]@{
        LocationName = $Location.Name
        OriginalPosition = [ordered]@{ X = [Math]::Round($Original.X, 2); Y = [Math]::Round($Original.Y, 2) }
        ExtendedPosition = [ordered]@{ X = [Math]::Round($Extended.X, 2); Y = [Math]::Round($Extended.Y, 2) }
        Angle = [Math]::Round($Angle, 2)
        LineLength = [Math]::Round([Math]::Sqrt(($dx * $dx) + ($dy * $dy)), 2)
    }
}

$resolvedConfigPath = (Resolve-Path $ConfigPath).Path
$resolvedExcelPath = (Resolve-Path $ExcelPath).Path
$resolvedMapPath = (Resolve-Path $MapImagePath).Path
$outputFullPath = Join-Path (Get-Location) $OutputPath
$outputDirectory = Split-Path -Parent $outputFullPath
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$config = Get-Content $resolvedConfigPath -Raw | ConvertFrom-Json
$locations = Get-ExcelLocations -Path $resolvedExcelPath

$bitmap = [System.Drawing.Bitmap]::FromFile($resolvedMapPath)
try {
    $mapWidth = [double]$bitmap.Width
    $mapHeight = [double]$bitmap.Height
}
finally {
    $bitmap.Dispose()
}

$radialConfig = New-Object SeedRadialConfig
$radialConfig.MinLocationsForExtension = [int]$config.RadialExtension.MinLocationsForExtension
$radialConfig.ProximityThresholdPixels = [double]$config.RadialExtension.ProximityThresholdPixels
$radialConfig.ExtensionLineLength = [double]$config.RadialExtension.ExtensionLineLength
$radialConfig.AngleNudgeThreshold = [double]$config.RadialExtension.AngleNudgeThreshold
$radialConfig.AngleNudgeAmount = [double]$config.RadialExtension.AngleNudgeAmount
$radialConfig.MinimumLineLength = [double]$config.RadialExtension.MinimumLineLength

$clusters = [SeedLayoutMath]::ClusterLocations($locations, [double]$config.ClusterDistanceThreshold)
$viewportSizes = @(
    @{ Width = 1920.0; Height = 1080.0 },  # 16:9
    @{ Width = 1440.0; Height = 900.0 },   # 16:10
    @{ Width = 1600.0; Height = 1200.0 },  # 4:3
    @{ Width = 3440.0; Height = 1440.0 }   # 21:9
)

# Load existing layout file to preserve Manual variants and SelectedVariants.
$existingLayoutGroups     = [ordered]@{}
$existingSelectedVariants = [ordered]@{}
if (Test-Path $outputFullPath) {
    try {
        $existingJson = Get-Content $outputFullPath -Raw | ConvertFrom-Json
        if ($null -ne $existingJson.LayoutGroups) {
            foreach ($prop in $existingJson.LayoutGroups.PSObject.Properties) {
                $existingLayoutGroups[$prop.Name] = $prop.Value
            }
        }
        if ($null -ne $existingJson.SelectedVariants) {
            foreach ($prop in $existingJson.SelectedVariants.PSObject.Properties) {
                $existingSelectedVariants[$prop.Name] = $prop.Value
            }
        }
        Write-Host "Loaded existing layout file: $($existingLayoutGroups.Count) groups, $($existingSelectedVariants.Count) selections."
    }
    catch {
        Write-Warning "Could not parse existing layout file; starting fresh. Error: $_"
    }
}

$layouts = [ordered]@{}
# Start from existing groups so Manual/Imported variants are preserved across seed regen.
$layoutGroups = [ordered]@{}
foreach ($k in $existingLayoutGroups.Keys) {
    $layoutGroups[$k] = $existingLayoutGroups[$k]
}
$multiLocationClusters = $clusters | Where-Object { $_.Locations.Count -gt 1 }

foreach ($cluster in $multiLocationClusters) {
    foreach ($size in $viewportSizes) {
        $viewport = [SeedViewport]::CreateZoomedView(
            [double]$cluster.CenterPoint.X,
            [double]$cluster.CenterPoint.Y,
            [double]$config.ZoomScale,
            $mapWidth,
            $mapHeight,
            [double]$size.Width,
            [double]$size.Height
        )

        $screenPositions = New-Object 'System.Collections.Generic.Dictionary[string,SeedPoint]'
        foreach ($location in $cluster.Locations) {
            $point = $viewport.SourceToScreen($location.PixelX, $location.PixelY, [double]$size.Width, [double]$size.Height)
            $screenPositions[$location.Id] = $point
        }

        $denseGroups = [SeedLayoutMath]::DetectDenseGroups(
            $cluster.Locations,
            $screenPositions,
            $radialConfig.MinLocationsForExtension,
            $radialConfig.ProximityThresholdPixels
        )

        $extensionByLocationId = @{}
        foreach ($denseGroup in $denseGroups) {
            $extensions = [SeedLayoutMath]::CalculateRadialExtensions(
                $denseGroup,
                $screenPositions,
                [double]$size.Width,
                [double]$size.Height,
                $radialConfig
            )

            foreach ($extension in $extensions) {
                $extensionByLocationId[$extension.Location.Id] = $extension
            }
        }

        $markers = @()
        $sortedLocations = $cluster.Locations | Sort-Object Name
        foreach ($location in $sortedLocations) {
            $original = $screenPositions[$location.Id]
            if ($extensionByLocationId.ContainsKey($location.Id)) {
                $extension = $extensionByLocationId[$location.Id]
                # Compute source-space extended position so the app can re-project to the
                # actual window viewport at load time (viewport-size-independent replay).
                $sourceExtended = $viewport.ScreenToSource(
                    $extension.ExtendedPosition.X,
                    $extension.ExtendedPosition.Y,
                    [double]$size.Width,
                    [double]$size.Height)
                $marker = New-LayoutMarker -Location $location -Original $extension.OriginalPosition -Extended $extension.ExtendedPosition -Angle $extension.Angle
                $marker["SourceExtendedX"] = [Math]::Round($sourceExtended.X, 4)
                $marker["SourceExtendedY"] = [Math]::Round($sourceExtended.Y, 4)
                $markers += $marker
            }
            else {
                $markers += New-LayoutMarker -Location $location -Original $original -Extended $original -Angle 0.0
            }
        }

        $key = [SeedLayoutMath]::GenerateLayoutKey($cluster.Locations, $viewport, $radialConfig)
        $timestamp = [DateTime]::UtcNow.ToString("o")
        $variant = [ordered]@{
            Key = $key
            GroupKey = $key
            VariantId = "seed-default"
            DisplayName = "Generated Seed"
            Origin = "AutoSeed"
            IsDefault = $true
            Timestamp = $timestamp
            CreatedUtc = $timestamp
            UpdatedUtc = $timestamp
            GeneratorVersion = "generate_manual_layout_seeds.ps1"
            LocationCount = $markers.Count
            Markers = $markers
        }
        $layouts[$key] = $variant

        # Merge seed-default variant into existing group, preserving Manual/Imported variants.
        if ($layoutGroups.Contains($key)) {
            $existingGroup = $layoutGroups[$key]
            $mergedVariants = New-Object System.Collections.ArrayList
            $seedReplaced   = $false
            foreach ($v in $existingGroup.Variants) {
                if ($v.VariantId -eq "seed-default" -and $v.Origin -eq "AutoSeed") {
                    [void]$mergedVariants.Add($variant)
                    $seedReplaced = $true
                } else {
                    [void]$mergedVariants.Add($v)
                }
            }
            if (-not $seedReplaced) {
                [void]$mergedVariants.Insert(0, $variant)
            }
            $layoutGroups[$key] = [ordered]@{
                GroupKey = $key
                Variants = $mergedVariants.ToArray()
            }
        } else {
            $layoutGroups[$key] = [ordered]@{
                GroupKey = $key
                Variants = @($variant)
            }
        }
    }
}

$payload = [ordered]@{
    LayoutGroups      = $layoutGroups
    SelectedVariants  = $existingSelectedVariants
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $outputFullPath -Encoding UTF8

Write-Host "Generated $($layouts.Count) seed variant(s) for $($multiLocationClusters.Count) multi-location clusters."
Write-Host "Saved: $outputFullPath"
