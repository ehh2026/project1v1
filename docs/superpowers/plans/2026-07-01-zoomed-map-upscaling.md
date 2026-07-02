# Zoomed Map Upscaling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate settled zoomed-map images at the monitor's physical pixel size, prevent incompatible cache reuse, and make five classical resampling treatments selectable through Runtime Tuning.

**Architecture:** Keep logical viewport and marker geometry in WPF device-independent units, but build an immutable settled-render request with physical output dimensions at the `MainWindow` composition boundary. Delegate custom pixel work to a focused separable resampler and cache identity to a focused key builder; `ZoomedRegionCache` continues to own source selection, crop mapping, PNG persistence, and fallbacks. Keep animation frames `Linear`, retain `Fant` as the shipping default, and add no graphics package.

**Tech Stack:** C# 10, .NET 6 WPF, `BitmapSource`/`CroppedBitmap`/`WriteableBitmap`, Newtonsoft.Json, xUnit, SHA-256, existing Runtime Tuning and verification harnesses.

**Approved design:** [2026-07-01-zoomed-map-upscaling-design.md](../specs/2026-07-01-zoomed-map-upscaling-design.md)

**Implementation status (July 2, 2026):** Tasks 1-7 are code complete. The
comparison generator produced all 15 1080p/1440p/4K outputs. Parallelized custom
generation measures about 1.7-3.3 seconds at 1440p and 3.7-8.8 seconds at 4K on
the development machine; `Fant` measures about 0.1-0.3 seconds. Automated full
gate components pass: clean Release build, 579 tests, NuGet audit, seed
verification, doc links, taste checks, and headless startup. The wrapper itself
timed out after leaving child `dotnet` processes, so its components were rerun
individually. Live WPF mode comparison/default selection remains Task 8.

---

## File and ownership map

**Create**

- `Models/ZoomedMapRenderConfig.cs` — persisted resampling enum/config only.
- `Models/ZoomedRegionRenderRequest.cs` — immutable settled render inputs.
- `Utilities/PhysicalPixelSizeCalculator.cs` — pure DIP-to-physical-pixel conversion.
- `Services/IZoomedMapResampler.cs` — cache-to-resampler contract.
- `Services/ZoomedMapResampler.cs` — Fant, Lanczos3, Mitchell-Netravali, bicubic, and sharpened-bicubic pixels.
- `Services/ZoomedRegionCacheKeyBuilder.cs` — source fingerprints and canonical SHA-256 cache keys.
- `MainWindow.MapRendering.partial.cs` — DPI-aware request construction at the composition boundary.
- `Tests/PhysicalPixelSizeCalculatorTests.cs`
- `Tests/ZoomedMapResamplerTests.cs`
- `Tests/ZoomedRegionCacheKeyBuilderTests.cs`
- `Tests/ZoomedMapRenderingWiringTests.cs`
- `Tools/MapResamplerComparison/MapResamplerComparison.csproj`
- `Tools/MapResamplerComparison/Program.cs`

**Modify**

- `Models/VisualConfig.cs` — add `ZoomedMapRendering`.
- `Models/TuningPanelEventArgs.cs` — carry the selected mode.
- `Services/VisualConfigService.cs` — preserve the rest of a config when an unknown mode is defaulted.
- `Services/ZoomedRegionCache.cs` — consume requests, physical dimensions, fingerprints, and the resampler.
- `MainWindow.xaml.cs` — initialize cache with both source paths and the config service warning sink.
- `MainWindow.Navigation.partial.cs` — replace positional cache calls with one request.
- `MainWindow.DeveloperTuning.partial.cs` — apply/reload the mode and replay a settled zoom.
- `Views/DeveloperTuningPanel.xaml` — add the Map-category combo box.
- `Views/DeveloperTuningPanel.xaml.cs` — load/build the selected enum.
- `visual-config.json` — add the default `Fant` mode.
- `Tests/VisualConfigServiceTests.cs`
- `Tests/ZoomedRegionCacheTests.cs`
- `Tests/TuningPanelWiringTests.cs`
- `Tests/MapImageRenderingPolicyTests.cs`
- `InteractiveWorldMap.sln` — include the comparison tool.
- `scripts/README.md` — document comparison generation.
- `docs/exec-plans/active/zoom-performance-appearance-plan.md`
- `docs/TO_DO.md`
- `CHANGELOG.md`

The main risk of bloat is `ZoomedRegionCache.cs`, currently about 239 lines.
Kernel math and canonical key construction must not be added there. If the
refactored cache exceeds 400 lines, stop and extract PNG load/save handling into
`Services/ZoomedRegionImageStore.cs` with focused tests before continuing.

---

### Task 1: Persist and safely validate the resampling mode

**Files:**

- Create: `Models/ZoomedMapRenderConfig.cs`
- Modify: `Models/VisualConfig.cs`
- Modify: `Services/VisualConfigService.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `visual-config.json`
- Test: `Tests/VisualConfigServiceTests.cs`

- [ ] **Step 1: Write failing config round-trip and unknown-value tests**

Append tests that prove string persistence and that one invalid mode does not
discard unrelated valid settings:

```csharp
[Fact]
public void ZoomedMapRendering_RoundTripsStringEnum()
{
    var path = Path.GetTempFileName();
    try
    {
        var service = new VisualConfigService();
        var config = new VisualConfig();
        config.ZoomedMapRendering.ResamplingMode =
            ZoomedMapResamplingMode.MitchellNetravali;

        service.Save(config, path);
        var json = File.ReadAllText(path);
        var reloaded = service.Load(path);

        Assert.Contains("\"ResamplingMode\": \"MitchellNetravali\"", json);
        Assert.Equal(
            ZoomedMapResamplingMode.MitchellNetravali,
            reloaded.ZoomedMapRendering.ResamplingMode);
    }
    finally
    {
        File.Delete(path);
    }
}

[Fact]
public void Load_UnknownZoomedMapMode_DefaultsOnlyModeAndWarns()
{
    var path = Path.GetTempFileName();
    var warnings = new List<string>();
    try
    {
        File.WriteAllText(path,
            "{ \"LocationMarkerSize\": 19.5, " +
            "\"ZoomedMapRendering\": { \"ResamplingMode\": \"FutureFilter\" } }");
        var service = new VisualConfigService(warnings.Add);

        var config = service.Load(path);

        Assert.Equal(19.5, config.LocationMarkerSize);
        Assert.Equal(
            ZoomedMapResamplingMode.Fant,
            config.ZoomedMapRendering.ResamplingMode);
        Assert.Single(warnings);
        Assert.Contains("FutureFilter", warnings[0]);
    }
    finally
    {
        File.Delete(path);
    }
}
```

- [ ] **Step 2: Run the two tests and confirm RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests.ZoomedMap" --no-restore
```

Expected: compilation fails because `ZoomedMapRenderConfig`,
`ZoomedMapResamplingMode`, and the warning-sink constructor do not exist.

- [ ] **Step 3: Add the enum and config model**

Create:

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace InteractiveWorldMap.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum ZoomedMapResamplingMode
{
    Fant,
    Lanczos3,
    MitchellNetravali,
    Bicubic,
    BicubicSharpened
}

public sealed class ZoomedMapRenderConfig
{
    public ZoomedMapResamplingMode ResamplingMode { get; set; } =
        ZoomedMapResamplingMode.Fant;
}
```

Add to `VisualConfig`:

```csharp
public ZoomedMapRenderConfig ZoomedMapRendering { get; set; } =
    new ZoomedMapRenderConfig();
```

Add to `visual-config.json` immediately after `AnimationDurationMs`:

```json
"ZoomedMapRendering": {
  "ResamplingMode": "Fant"
},
```

- [ ] **Step 4: Normalize only an unknown zoomed-map enum during load**

Change `VisualConfigService` to accept an optional warning sink and normalize
the one new property before `ToObject`:

```csharp
using Newtonsoft.Json.Linq;

private readonly Action<string>? _warningSink;

public VisualConfigService(Action<string>? warningSink = null)
{
    _warningSink = warningSink;
}

public VisualConfig Load(string filePath)
{
    EnsureConfigExists(filePath);

    try
    {
        var root = JObject.Parse(File.ReadAllText(filePath));
        var modeToken = root["ZoomedMapRendering"]?["ResamplingMode"];
        if (modeToken?.Type == JTokenType.String)
        {
            var text = modeToken.Value<string>() ?? string.Empty;
            if (!Enum.TryParse<ZoomedMapResamplingMode>(
                    text, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(typeof(ZoomedMapResamplingMode), parsed))
            {
                _warningSink?.Invoke(
                    $"Unknown zoomed-map resampling mode '{text}'; using Fant.");
                modeToken.Replace(ZoomedMapResamplingMode.Fant.ToString());
            }
        }

        return root.ToObject<VisualConfig>() ?? new VisualConfig();
    }
    catch (Exception ex)
    {
        _warningSink?.Invoke(
            $"Failed to load visual configuration; using defaults: {ex.Message}");
        return new VisualConfig();
    }
}
```

In `MainWindow.xaml.cs`, remove the field initializer:

```csharp
private readonly VisualConfigService _configService;
```

Then initialize it after `_logger = new FileLogger();` and before loading
`visual-config.json`:

```csharp
_configService = new VisualConfigService(message => _logger.LogWarning(message));
```

- [ ] **Step 5: Run focused config tests and confirm GREEN**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~VisualConfigServiceTests" --no-restore
```

Expected: all `VisualConfigServiceTests` pass; serialized JSON contains the enum
name rather than its integer value.

- [ ] **Step 6: Commit the config slice**

```powershell
git add Models\ZoomedMapRenderConfig.cs Models\VisualConfig.cs Services\VisualConfigService.cs MainWindow.xaml.cs visual-config.json Tests\VisualConfigServiceTests.cs
git commit -m "feat: configure zoomed map resampling mode"
```

---

### Task 2: Calculate physical output pixels without changing viewport geometry

**Files:**

- Create: `Utilities/PhysicalPixelSizeCalculator.cs`
- Create: `Tests/PhysicalPixelSizeCalculatorTests.cs`

- [ ] **Step 1: Write failing conversion tests**

Create:

```csharp
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class PhysicalPixelSizeCalculatorTests
{
    [Theory]
    [InlineData(1920, 1080, 1.0, 1.0, 1920, 1080)]
    [InlineData(1706.6667, 960, 1.5, 1.5, 2560, 1440)]
    [InlineData(1536, 864, 1.25, 1.25, 1920, 1080)]
    public void TryCalculate_ValidDipsAndDpi_ReturnsPhysicalPixels(
        double dipWidth, double dipHeight,
        double dpiX, double dpiY,
        int expectedWidth, int expectedHeight)
    {
        var ok = PhysicalPixelSizeCalculator.TryCalculate(
            dipWidth, dipHeight, dpiX, dpiY,
            out var width, out var height);

        Assert.True(ok);
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Theory]
    [InlineData(0, 100, 1, 1)]
    [InlineData(100, double.NaN, 1, 1)]
    [InlineData(100, 100, 0, 1)]
    [InlineData(100, 100, 1, double.PositiveInfinity)]
    public void TryCalculate_InvalidInput_ReturnsFalse(
        double width, double height, double dpiX, double dpiY)
    {
        Assert.False(PhysicalPixelSizeCalculator.TryCalculate(
            width, height, dpiX, dpiY, out _, out _));
    }
}
```

- [ ] **Step 2: Run the tests and confirm RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~PhysicalPixelSizeCalculatorTests" --no-restore
```

Expected: compilation fails because the calculator does not exist.

- [ ] **Step 3: Implement the pure calculator**

Create:

```csharp
using System;

namespace InteractiveWorldMap.Utilities;

public static class PhysicalPixelSizeCalculator
{
    public static bool TryCalculate(
        double dipWidth,
        double dipHeight,
        double dpiScaleX,
        double dpiScaleY,
        out int pixelWidth,
        out int pixelHeight)
    {
        pixelWidth = 0;
        pixelHeight = 0;

        if (!double.IsFinite(dipWidth) || !double.IsFinite(dipHeight) ||
            !double.IsFinite(dpiScaleX) || !double.IsFinite(dpiScaleY) ||
            dipWidth <= 0 || dipHeight <= 0 ||
            dpiScaleX <= 0 || dpiScaleY <= 0)
        {
            return false;
        }

        var width = Math.Round(
            dipWidth * dpiScaleX, MidpointRounding.AwayFromZero);
        var height = Math.Round(
            dipHeight * dpiScaleY, MidpointRounding.AwayFromZero);

        if (width < 1 || width > int.MaxValue ||
            height < 1 || height > int.MaxValue)
        {
            return false;
        }

        pixelWidth = (int)width;
        pixelHeight = (int)height;
        return true;
    }
}
```

- [ ] **Step 4: Run the tests and confirm GREEN**

Run the command from Step 2. Expected: all conversion cases pass.

- [ ] **Step 5: Commit the DPI calculation slice**

```powershell
git add Utilities\PhysicalPixelSizeCalculator.cs Tests\PhysicalPixelSizeCalculatorTests.cs
git commit -m "feat: calculate physical map render dimensions"
```

---

### Task 3: Implement deterministic classical resamplers

**Files:**

- Create: `Services/IZoomedMapResampler.cs`
- Create: `Services/ZoomedMapResampler.cs`
- Create: `Tests/ZoomedMapResamplerTests.cs`
- Modify: `Tests/MapImageRenderingPolicyTests.cs`

- [ ] **Step 1: Write failing output-contract tests**

Create tests using 1x1 constant, 4x4 gradient, and 8x4 black/white step fixtures:

```csharp
[Theory]
[InlineData(ZoomedMapResamplingMode.Fant)]
[InlineData(ZoomedMapResamplingMode.Lanczos3)]
[InlineData(ZoomedMapResamplingMode.MitchellNetravali)]
[InlineData(ZoomedMapResamplingMode.Bicubic)]
[InlineData(ZoomedMapResamplingMode.BicubicSharpened)]
public void Resize_AllModes_ReturnRequestedFrozenBitmap(
    ZoomedMapResamplingMode mode)
{
    var result = new ZoomedMapResampler().Resize(
        CreateGradient(4, 4), 17, 11, mode);

    Assert.Equal(17, result.PixelWidth);
    Assert.Equal(11, result.PixelHeight);
    Assert.True(result.IsFrozen);
}

[Theory]
[InlineData(ZoomedMapResamplingMode.Lanczos3)]
[InlineData(ZoomedMapResamplingMode.MitchellNetravali)]
[InlineData(ZoomedMapResamplingMode.Bicubic)]
[InlineData(ZoomedMapResamplingMode.BicubicSharpened)]
public void Resize_ConstantOnePixel_RemainsConstant(
    ZoomedMapResamplingMode mode)
{
    var source = CreateSolid(1, 1, b: 17, g: 89, r: 201, a: 255);
    var result = new ZoomedMapResampler().Resize(source, 19, 13, mode);

    Assert.All(ReadPixels(result).Chunk(4), pixel =>
    {
        Assert.Equal((byte)17, pixel[0]);
        Assert.Equal((byte)89, pixel[1]);
        Assert.Equal((byte)201, pixel[2]);
        Assert.Equal((byte)255, pixel[3]);
    });
}

[Theory]
[InlineData(ZoomedMapResamplingMode.Lanczos3)]
[InlineData(ZoomedMapResamplingMode.MitchellNetravali)]
[InlineData(ZoomedMapResamplingMode.Bicubic)]
[InlineData(ZoomedMapResamplingMode.BicubicSharpened)]
public void Resize_CustomModes_AreDeterministic(
    ZoomedMapResamplingMode mode)
{
    var source = CreateGradient(5, 3);
    var first = new ZoomedMapResampler().Resize(source, 23, 17, mode);
    var second = new ZoomedMapResampler().Resize(source, 23, 17, mode);

    Assert.Equal(ReadPixels(first), ReadPixels(second));
}

[Fact]
public void BicubicSharpened_IncreasesImmediateStepContrastWithoutWideHalo()
{
    var source = CreateVerticalStep(width: 8, height: 4);
    var resampler = new ZoomedMapResampler();
    var bicubic = ReadRow(
        resampler.Resize(source, 64, 8, ZoomedMapResamplingMode.Bicubic), 4);
    var sharpened = ReadRow(
        resampler.Resize(source, 64, 8, ZoomedMapResamplingMode.BicubicSharpened), 4);

    Assert.True(LocalEdgeContrast(sharpened, 32) >
                LocalEdgeContrast(bicubic, 32));
    Assert.InRange(sharpened[8], 0, 8);
    Assert.InRange(sharpened[55], 247, 255);
}
```

Implement test helpers in the same file with `BitmapSource.Create`,
`FormatConvertedBitmap`, and `CopyPixels`; do not load repository map assets in
unit tests.

- [ ] **Step 2: Run resampler tests and confirm RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ZoomedMapResamplerTests" --no-restore
```

Expected: compilation fails because the resampler contract and implementation
do not exist.

- [ ] **Step 3: Define the resampler contract**

Create:

```csharp
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public interface IZoomedMapResampler
{
    BitmapSource Resize(
        BitmapSource source,
        int outputWidth,
        int outputHeight,
        ZoomedMapResamplingMode mode);
}
```

- [ ] **Step 4: Implement mode dispatch and Fant compatibility**

Start `ZoomedMapResampler` with strict validation:

```csharp
public sealed class ZoomedMapResampler : IZoomedMapResampler
{
    public const int PolicyVersion = 1;
    private const double SharpenAmount = 0.25;
    private const byte SharpenThreshold = 2;

    private enum KernelKind
    {
        Lanczos3,
        Mitchell,
        CatmullRom
    }

    public BitmapSource Resize(
        BitmapSource source,
        int outputWidth,
        int outputHeight,
        ZoomedMapResamplingMode mode)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (outputWidth <= 0) throw new ArgumentOutOfRangeException(nameof(outputWidth));
        if (outputHeight <= 0) throw new ArgumentOutOfRangeException(nameof(outputHeight));

        if (mode == ZoomedMapResamplingMode.Fant)
            return ResizeFant(source, outputWidth, outputHeight);

        var kernel = mode switch
        {
            ZoomedMapResamplingMode.Lanczos3 => KernelKind.Lanczos3,
            ZoomedMapResamplingMode.MitchellNetravali => KernelKind.Mitchell,
            ZoomedMapResamplingMode.Bicubic => KernelKind.CatmullRom,
            ZoomedMapResamplingMode.BicubicSharpened => KernelKind.CatmullRom,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        var resized = ResizeSeparable(source, outputWidth, outputHeight, kernel);
        return mode == ZoomedMapResamplingMode.BicubicSharpened
            ? ApplyUnsharpMask(resized)
            : resized;
    }

    private static BitmapSource ResizeFant(
        BitmapSource source, int outputWidth, int outputHeight)
    {
        var scaled = new TransformedBitmap(
            source,
            new ScaleTransform(
                outputWidth / (double)source.PixelWidth,
                outputHeight / (double)source.PixelHeight));
        RenderOptions.SetBitmapScalingMode(scaled, BitmapScalingMode.Fant);
        var materialized = new WriteableBitmap(scaled);
        materialized.Freeze();
        return materialized;
    }
}
```

- [ ] **Step 5: Implement exact kernels and normalized contributor tables**

Use destination-pixel-center mapping
`sourcePosition = ((destination + 0.5) / scale) - 0.5`. Implement these exact
kernels:

```csharp
private static double Sinc(double x)
{
    if (Math.Abs(x) < 1e-12) return 1.0;
    var p = Math.PI * x;
    return Math.Sin(p) / p;
}

private static double Lanczos3(double x)
{
    x = Math.Abs(x);
    return x < 3.0 ? Sinc(x) * Sinc(x / 3.0) : 0.0;
}

private static double Mitchell(double x)
{
    const double b = 1.0 / 3.0;
    const double c = 1.0 / 3.0;
    x = Math.Abs(x);
    if (x < 1.0)
    {
        return ((12 - 9 * b - 6 * c) * x * x * x +
                (-18 + 12 * b + 6 * c) * x * x +
                (6 - 2 * b)) / 6.0;
    }
    if (x < 2.0)
    {
        return ((-b - 6 * c) * x * x * x +
                (6 * b + 30 * c) * x * x +
                (-12 * b - 48 * c) * x +
                (8 * b + 24 * c)) / 6.0;
    }
    return 0.0;
}

private static double CatmullRom(double x)
{
    const double a = -0.5;
    x = Math.Abs(x);
    if (x <= 1.0)
        return ((a + 2) * x - (a + 3)) * x * x + 1;
    if (x < 2.0)
        return (((a * x - 5 * a) * x + 8 * a) * x - 4 * a);
    return 0.0;
}
```

For each destination coordinate, enumerate `floor(position-radius+1)` through
`floor(position+radius)`, clamp each index to the valid source range, merge
duplicate clamped indices, and divide every weight by the sum. If the absolute
sum is below `1e-12`, use the nearest clamped source index with weight 1.

For completeness when a custom mode receives a downscale, widen support with
`filterScale = Math.Min(1.0, destinationLength / (double)sourceLength)`,
`effectiveRadius = radius / filterScale`, and evaluate each raw weight as
`kernel((position - sourceIndex) * filterScale) * filterScale`. Upscaling keeps
`filterScale = 1.0`.

- [ ] **Step 6: Implement the two-pass BGRA32 resize**

Convert once to `PixelFormats.Bgra32`. The horizontal pass writes a
`double[]` sized `outputWidth * sourceHeight * 4`; the vertical pass writes the
final byte buffer. For every channel:

```csharp
var value = 0.0;
foreach (var contribution in contributors[destination])
    value += input[inputOffset + contribution.SourceIndex * 4 + channel]
             * contribution.Weight;

output[outputOffset + channel] =
    (byte)Math.Clamp(
        Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
```

Create the result with 96 DPI and `PixelFormats.Bgra32`, then freeze it. Keep
the implementation single-threaded for deterministic first measurements; do
not add `Parallel.For` in this slice.

- [ ] **Step 7: Implement restrained unsharp masking**

Apply the normalized 3x3 Gaussian kernel
`[1 2 1; 2 4 2; 1 2 1] / 16` with clamped borders. For B, G, and R:

```csharp
var difference = original - blurred;
var sharpened = Math.Abs(difference) < SharpenThreshold
    ? original
    : original + (SharpenAmount * difference);
result = (byte)Math.Clamp(
    Math.Round(sharpened, MidpointRounding.AwayFromZero), 0, 255);
```

Copy alpha unchanged. Return a new frozen BGRA32 bitmap.

- [ ] **Step 8: Preserve the animation policy guard**

Extend `MapImageRenderingPolicyTests` to assert that
`MainWindow.Navigation.partial.cs` still contains
`BitmapScalingMode.Linear` in keyframe generation and that custom modes appear
only in `ZoomedMapResampler.cs`.

- [ ] **Step 9: Run focused resampler and policy tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ZoomedMapResamplerTests|FullyQualifiedName~MapImageRenderingPolicyTests" --no-restore
```

Expected: all mode, fixture, determinism, sharpening, and animation-policy tests
pass.

- [ ] **Step 10: Commit the resampler slice**

```powershell
git add Services\IZoomedMapResampler.cs Services\ZoomedMapResampler.cs Tests\ZoomedMapResamplerTests.cs Tests\MapImageRenderingPolicyTests.cs
git commit -m "feat: add zoomed map resampling algorithms"
```

---

### Task 4: Make settled-region cache identity complete

**Files:**

- Create: `Models/ZoomedRegionRenderRequest.cs`
- Create: `Services/ZoomedRegionCacheKeyBuilder.cs`
- Create: `Tests/ZoomedRegionCacheKeyBuilderTests.cs`
- Modify: `Services/ZoomedRegionCache.cs`
- Modify: `Tests/ZoomedRegionCacheTests.cs`

- [ ] **Step 1: Write failing key-separation tests**

Create a baseline request and fingerprint, then use a theory that changes one
input at a time:

```csharp
[Fact]
public void BuildKey_IsStableForSameInputs()
{
    var builder = new ZoomedRegionCacheKeyBuilder();
    Assert.Equal(
        builder.Build(BaselineRequest(), BaselineSource()),
        builder.Build(BaselineRequest(), BaselineSource()));
}

[Theory]
[InlineData("source-role")]
[InlineData("source-path")]
[InlineData("source-length")]
[InlineData("source-write-time")]
[InlineData("pixel-width")]
[InlineData("dpi")]
[InlineData("mode")]
[InlineData("center-fraction")]
public void BuildKey_ChangesWhenCompatibilityInputChanges(string change)
{
    var originalRequest = BaselineRequest();
    var originalSource = BaselineSource();
    var changedRequest = ChangeRequest(originalRequest, change);
    var changedSource = ChangeSource(originalSource, change);
    var builder = new ZoomedRegionCacheKeyBuilder();

    Assert.NotEqual(
        builder.Build(originalRequest, originalSource),
        builder.Build(changedRequest, changedSource));
}
```

`center-fraction` must compare values that were previously collapsed by `F1`,
for example `100.01` and `100.04`.

- [ ] **Step 2: Write failing cache behavior tests**

Refactor `ZoomedRegionCacheTests` to use one unique injected cache directory per
test and add:

```csharp
[Fact]
public void FallbackCache_DoesNotMaskFullSourceWhenItAppears()
{
    using var fixture = new CacheFixture();
    var fallback = CreateSolidBitmap(2, 2, r: 20, g: 30, b: 40);
    SaveBitmap(fallback, fixture.FallbackPath);
    var request = CreateRequest(ZoomedMapResamplingMode.Fant);

    var fallbackCache = fixture.CreateCache();
    fallbackCache.GenerateAndCacheRegion(fallback, request);

    SaveBitmap(
        CreateSolidBitmap(2, 2, r: 200, g: 150, b: 100),
        fixture.FullPath);
    var fullCache = fixture.CreateCache();

    Assert.Null(fullCache.TryLoadRegion(request));
    var generated = fullCache.GenerateAndCacheRegion(fallback, request);
    Assert.Equal((byte)200, ReadFirstPixel(generated).R);
}

[Fact]
public void TryLoadRegion_CorruptPng_IsDeletedAndReturnsMiss()
{
    using var fixture = new CacheFixture();
    var source = CreateSolidBitmap(2, 2, r: 80, g: 90, b: 100);
    SaveBitmap(source, fixture.FullPath);
    SaveBitmap(source, fixture.FallbackPath);
    var request = CreateRequest(ZoomedMapResamplingMode.Fant);
    var cache = fixture.CreateCache();
    cache.GenerateAndCacheRegion(source, request);
    var png = Directory.GetFiles(fixture.CacheDirectory, "*.png").Single();
    File.WriteAllBytes(png, new byte[] { 1, 2, 3, 4 });

    Assert.Null(cache.TryLoadRegion(request));
    Assert.False(File.Exists(png));
}

[Fact]
public void Generate_CustomFailure_DisplaysFantWithoutMislabelingCache()
{
    using var fixture = new CacheFixture();
    var source = CreateSolidBitmap(2, 2, r: 120, g: 130, b: 140);
    SaveBitmap(source, fixture.FullPath);
    SaveBitmap(source, fixture.FallbackPath);
    var logger = new MockLogger();
    var request = CreateRequest(ZoomedMapResamplingMode.Lanczos3);
    var cache = fixture.CreateCache(
        logger,
        new ThrowOneModeResampler(ZoomedMapResamplingMode.Lanczos3));

    var result = cache.GenerateAndCacheRegion(source, request);

    Assert.Equal(request.PixelWidth, result.PixelWidth);
    Assert.Equal(request.PixelHeight, result.PixelHeight);
    Assert.Contains(
        logger.WarningMessages,
        message => message.Contains("Lanczos3", StringComparison.Ordinal));

    var builder = new ZoomedRegionCacheKeyBuilder();
    var fingerprint = builder.Fingerprint("full-resolution", fixture.FullPath);
    var requestedPath = Path.Combine(
        fixture.CacheDirectory,
        builder.Build(request, fingerprint) + ".png");
    var fantPath = Path.Combine(
        fixture.CacheDirectory,
        builder.Build(
            request,
            fingerprint,
            ZoomedMapResamplingMode.Fant) + ".png");
    Assert.False(File.Exists(requestedPath));
    Assert.True(File.Exists(fantPath));
}
```

Define `CacheFixture` in the test file to create and recursively delete a unique
temporary root, expose `FullPath`, `FallbackPath`, and `CacheDirectory`, and
construct `ZoomedRegionCache` with those paths. Define
`ThrowOneModeResampler` as an `IZoomedMapResampler` decorator over
`ZoomedMapResampler` that throws only for its configured mode. Reuse the
existing bitmap save/read helpers rather than duplicating encoder code.

- [ ] **Step 3: Run key/cache tests and confirm RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ZoomedRegionCacheKeyBuilderTests|FullyQualifiedName~ZoomedRegionCacheTests" --no-restore
```

Expected: compilation fails because request/key types and new cache signatures do
not exist.

- [ ] **Step 4: Add the immutable request**

Create:

```csharp
using System.Windows;

namespace InteractiveWorldMap.Models;

public sealed record ZoomedRegionRenderRequest(
    double CenterX,
    double CenterY,
    double ZoomLevel,
    int PixelWidth,
    int PixelHeight,
    double DpiScaleX,
    double DpiScaleY,
    ZoomedMapResamplingMode ResamplingMode,
    Int32Rect HalfResSourceRect);
```

The constructor call sites must reject invalid dimensions before constructing
this record; cache methods also validate defensively.

- [ ] **Step 5: Implement source fingerprints and canonical keys**

Create `ZoomedRegionCacheKeyBuilder` with:

```csharp
public sealed record ZoomedRegionSourceFingerprint(
    string Role,
    string NormalizedPath,
    long Length,
    long LastWriteTimeUtcTicks);

public sealed class ZoomedRegionCacheKeyBuilder
{
    public const int CacheSchemaVersion = 8;

    public ZoomedRegionSourceFingerprint Fingerprint(string role, string path)
    {
        var info = new FileInfo(Path.GetFullPath(path));
        return new ZoomedRegionSourceFingerprint(
            role,
            info.FullName,
            info.Exists ? info.Length : -1,
            info.Exists ? info.LastWriteTimeUtc.Ticks : -1);
    }

    public string Build(
        ZoomedRegionRenderRequest request,
        ZoomedRegionSourceFingerprint source,
        ZoomedMapResamplingMode? actualMode = null)
    {
        var mode = actualMode ?? request.ResamplingMode;
        var canonical = string.Join("|",
            $"schema={CacheSchemaVersion}",
            $"policy={ZoomedMapResampler.PolicyVersion}",
            $"cx={request.CenterX.ToString("R", CultureInfo.InvariantCulture)}",
            $"cy={request.CenterY.ToString("R", CultureInfo.InvariantCulture)}",
            $"zoom={request.ZoomLevel.ToString("R", CultureInfo.InvariantCulture)}",
            $"pixels={request.PixelWidth}x{request.PixelHeight}",
            $"dpi={request.DpiScaleX.ToString("R", CultureInfo.InvariantCulture)}," +
                request.DpiScaleY.ToString("R", CultureInfo.InvariantCulture),
            $"mode={mode}",
            $"role={source.Role}",
            $"path={source.NormalizedPath}",
            $"length={source.Length}",
            $"write={source.LastWriteTimeUtcTicks}");

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24];
    }
}
```

The public `ZoomedMapResampler.PolicyVersion` constant lets the key builder
reference pixel-policy state without duplicating it.

- [ ] **Step 6: Refactor cache construction and public methods**

Use this constructor:

```csharp
public ZoomedRegionCache(
    ILogger logger,
    string fullResolutionImagePath,
    string fallbackImagePath,
    IZoomedMapResampler? resampler = null,
    string? cacheDirectory = null)
```

Store both paths, default `resampler` to `new ZoomedMapResampler()`, default the
directory to the existing AppData location, and instantiate one key builder.
Replace public positional methods with:

```csharp
public BitmapSource? TryLoadRegion(ZoomedRegionRenderRequest request)

public BitmapSource GenerateAndCacheRegion(
    BitmapSource halfResSource,
    ZoomedRegionRenderRequest request)
```

Set the cache version file to
`ZoomedRegionCacheKeyBuilder.CacheSchemaVersion`.

- [ ] **Step 7: Make selected source identity authoritative**

Before lookup, choose full source only when its file exists and it has not
failed to load in this cache instance. Otherwise choose the supplied fallback
path/bitmap and the role `fallback`. If full loading throws, log a warning, set
`_fullResolutionUnavailableForSession = true`, recompute the fallback
fingerprint/key, and continue from the half-resolution bitmap.

Scale `request.HalfResSourceRect` into the full source with the existing
per-axis ratios and clamping. Pass the resulting `CroppedBitmap` plus
`request.PixelWidth`, `request.PixelHeight`, and the requested mode to the
resampler.

- [ ] **Step 8: Implement cache-read and fallback correctness**

On cached PNG decode failure:

```csharp
catch (Exception ex)
{
    _logger.LogWarning($"Cached zoomed region is invalid; regenerating: {ex.Message}");
    try { File.Delete(cachePath); }
    catch (Exception deleteEx)
    {
        _logger.LogWarning(
            $"Could not delete invalid zoomed region cache file: {deleteEx.Message}");
    }
    return null;
}
```

On custom resampler failure, call the same resampler with `Fant`, build the save
key using `actualMode: ZoomedMapResamplingMode.Fant`, and do not write the
requested-mode key. On PNG save failure, log and return the frozen in-memory
result.

- [ ] **Step 9: Run focused cache tests and confirm GREEN**

Run the command from Step 3. Expected: all existing crop/fallback/cache-hit
tests plus key separation, corrupt PNG, source appearance, and actual-mode
fallback tests pass.

- [ ] **Step 10: Commit the cache slice**

```powershell
git add Models\ZoomedRegionRenderRequest.cs Services\ZoomedRegionCacheKeyBuilder.cs Services\ZoomedRegionCache.cs Services\ZoomedMapResampler.cs Tests\ZoomedRegionCacheKeyBuilderTests.cs Tests\ZoomedRegionCacheTests.cs
git commit -m "feat: key zoomed map cache by render identity"
```

---

### Task 5: Wire physical-pixel settled rendering into navigation

**Files:**

- Create: `MainWindow.MapRendering.partial.cs`
- Create: `Tests/ZoomedMapRenderingWiringTests.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `MainWindow.Navigation.partial.cs`

- [ ] **Step 1: Write failing source-boundary tests**

Create structural tests that protect the composition contract:

```csharp
[Fact]
public void MapRenderingRequest_UsesVisualDpiAndPhysicalPixelCalculator()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.MapRendering.partial.cs"));

    Assert.Contains("VisualTreeHelper.GetDpi(MapDisplay)", source);
    Assert.Contains("PhysicalPixelSizeCalculator.TryCalculate(", source);
    Assert.Contains("_visualConfig.ZoomedMapRendering.ResamplingMode", source);
}

[Fact]
public void Navigation_UsesOneRenderRequestForCacheLoadAndGeneration()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.Navigation.partial.cs"));

    Assert.Contains("_zoomedRegionCache.TryLoadRegion(request)", source);
    Assert.Contains(
        "_zoomedRegionCache.GenerateAndCacheRegion(sourceImage, request)",
        source);
    Assert.DoesNotContain(
        "var displayWidth = (int)MapDisplay.ActualWidth", source);
}

[Fact]
public void CacheConstruction_ReceivesFullAndFallbackPaths()
{
    var source = File.ReadAllText(Path.Combine(RepoRoot, "MainWindow.xaml.cs"));
    Assert.Contains("_contentLoader.GetFullResolutionWorldMapPath()", source);
    Assert.Contains("_contentLoader.GetWorldMapPath()", source);
}
```

- [ ] **Step 2: Run wiring tests and confirm RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ZoomedMapRenderingWiringTests" --no-restore
```

Expected: tests fail because the partial file/request wiring does not exist.

- [ ] **Step 3: Build requests in a focused MainWindow partial**

Create:

```csharp
using System.Windows.Media;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap;

public partial class MainWindow
{
    private ZoomedRegionRenderRequest? TryCreateZoomedRegionRenderRequest(
        ViewportState viewport,
        double centerX,
        double centerY)
    {
        var dpi = VisualTreeHelper.GetDpi(MapDisplay);
        if (!PhysicalPixelSizeCalculator.TryCalculate(
                MapDisplay.ActualWidth,
                MapDisplay.ActualHeight,
                dpi.DpiScaleX,
                dpi.DpiScaleY,
                out var pixelWidth,
                out var pixelHeight))
        {
            _logger.LogWarning(
                "Skipping settled zoomed-map render because display dimensions are invalid.");
            return null;
        }

        return new ZoomedRegionRenderRequest(
            centerX,
            centerY,
            ZoomScale,
            pixelWidth,
            pixelHeight,
            dpi.DpiScaleX,
            dpi.DpiScaleY,
            _visualConfig.ZoomedMapRendering.ResamplingMode,
            viewport.GetSourceRect());
    }
}
```

- [ ] **Step 4: Replace navigation's positional cache calls**

Inside `ShowZoomedView`, construct one request. If it is null, keep the
animation's final displayed frame and continue marker/layout settle logic. If
non-null:

```csharp
var request = TryCreateZoomedRegionRenderRequest(
    viewport, cluster.CenterPoint.X, cluster.CenterPoint.Y);

if (request != null)
{
    var cachedRegion = _zoomedRegionCache.TryLoadRegion(request);
    if (cachedRegion != null)
    {
        MapDisplay.DisplayImage.Source = cachedRegion;
    }
    else if (MapDisplay.SourceImage is BitmapSource sourceImage)
    {
        MapDisplay.DisplayImage.Source =
            _zoomedRegionCache.GenerateAndCacheRegion(sourceImage, request);
    }
}
```

Keep existing info logs, but include mode and physical dimensions in the
generation log.

- [ ] **Step 5: Pass both source paths to the cache**

Replace construction in `MainWindow.xaml.cs` with:

```csharp
_zoomedRegionCache = new ZoomedRegionCache(
    _logger,
    _contentLoader.GetFullResolutionWorldMapPath(),
    _contentLoader.GetWorldMapPath());
```

- [ ] **Step 6: Run wiring, cache, viewport, and marker regressions**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ZoomedMapRenderingWiringTests|FullyQualifiedName~ZoomedRegionCacheTests|FullyQualifiedName~ViewportStateTests|FullyQualifiedName~MarkerPlacementOrchestratorTests" --no-restore
```

Expected: all tests pass; no viewport or marker math has changed.

- [ ] **Step 7: Commit the navigation slice**

```powershell
git add MainWindow.MapRendering.partial.cs MainWindow.xaml.cs MainWindow.Navigation.partial.cs Tests\ZoomedMapRenderingWiringTests.cs
git commit -m "feat: render settled zoom at physical pixel size"
```

---

### Task 6: Expose every mode through Runtime Tuning

**Files:**

- Modify: `Models/TuningPanelEventArgs.cs`
- Modify: `Views/DeveloperTuningPanel.xaml`
- Modify: `Views/DeveloperTuningPanel.xaml.cs`
- Modify: `MainWindow.DeveloperTuning.partial.cs`
- Modify: `Tests/TuningPanelWiringTests.cs`
- Modify: `Tests/TuningReloadValidationTests.cs`

- [ ] **Step 1: Write failing tuning wiring tests**

Add:

```csharp
[Fact]
public void DeveloperTuningPanel_ZoomedMapCombo_HasEveryMode()
{
    var xaml = File.ReadAllText(
        Path.Combine(RepoRoot, "Views", "DeveloperTuningPanel.xaml"));

    Assert.Contains("x:Name=\"CmbZoomedMapResampling\"", xaml);
    foreach (var mode in Enum.GetNames<ZoomedMapResamplingMode>())
        Assert.Contains($"<ComboBoxItem Content=\"{mode}\"/>", xaml);
}

[Fact]
public void ApplyTuning_MapsZoomedMapModeAndReplaysView()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "MainWindow.DeveloperTuning.partial.cs"));

    Assert.Contains(
        "_visualConfig.ZoomedMapRendering.ResamplingMode = e.ZoomedMapResamplingMode;",
        source);
    Assert.Contains("ReapplyViewAfterTuningChange()", source);
    Assert.Contains(
        "ZoomedMapResamplingMode = config.ZoomedMapRendering.ResamplingMode",
        source);
}
```

Extend the existing tooltip test to include `CmbZoomedMapResampling`.

- [ ] **Step 2: Run tuning tests and confirm RED**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests" --no-restore
```

Expected: new tests fail because the combo/event field/mapping is absent.

- [ ] **Step 3: Add the event argument and Map-category combo**

Add to `TuningPanelEventArgs`:

```csharp
public ZoomedMapResamplingMode ZoomedMapResamplingMode { get; set; } =
    ZoomedMapResamplingMode.Fant;
```

Add under the Map section's existing controls:

```xml
<TextBlock Text="Zoomed map resampling"
           Style="{StaticResource TuningLabelStyle}"
           Margin="0,8,0,1"/>
<ComboBox x:Name="CmbZoomedMapResampling"
          IsEditable="False"
          Background="#222222"
          Foreground="White"
          SelectionChanged="OnVariantSelectionChanged"
          ItemContainerStyle="{StaticResource DarkComboBoxItemStyle}"
          ToolTip="Resampling used only after zoom settles; animation remains Linear.">
    <ComboBoxItem Content="Fant"/>
    <ComboBoxItem Content="Lanczos3"/>
    <ComboBoxItem Content="MitchellNetravali"/>
    <ComboBoxItem Content="Bicubic"/>
    <ComboBoxItem Content="BicubicSharpened"/>
</ComboBox>
```

- [ ] **Step 4: Load and parse the enum in the View**

Add focused helpers analogous to the tip-cap enum helpers:

```csharp
private void SetZoomedMapResamplingMode(ZoomedMapResamplingMode mode)
{
    foreach (var item in CmbZoomedMapResampling.Items)
    {
        if (item is ComboBoxItem comboItem &&
            string.Equals(
                comboItem.Content?.ToString(),
                mode.ToString(),
                StringComparison.Ordinal))
        {
            CmbZoomedMapResampling.SelectedItem = comboItem;
            return;
        }
    }
    CmbZoomedMapResampling.SelectedIndex = 0;
}

private ZoomedMapResamplingMode GetZoomedMapResamplingMode()
{
    var text = (CmbZoomedMapResampling.SelectedItem as ComboBoxItem)?
        .Content?.ToString();
    return Enum.TryParse<ZoomedMapResamplingMode>(text, out var mode)
        ? mode
        : ZoomedMapResamplingMode.Fant;
}
```

Call the setter from `LoadValues` and set the event argument from the getter in
`TryBuildEventArgs`.

- [ ] **Step 5: Apply, reload, and replay mode changes**

In `CreateTuningArgs`, copy config mode into the event. In `ApplyTuningAsync`,
capture the old mode, assign the new mode, and include mode changes in
`renderSettingsChanged`:

```csharp
var oldZoomedMapMode =
    _visualConfig.ZoomedMapRendering.ResamplingMode;

var renderSettingsChanged =
    oldUsePrerasterize != e.UsePrerasterize ||
    oldShowDebugOverlay != e.ShowDebugOverlay ||
    oldZoomedMapMode != e.ZoomedMapResamplingMode;

_visualConfig.ZoomedMapRendering.ResamplingMode =
    e.ZoomedMapResamplingMode;
```

Do not clear all zoom-region files. `ReapplyViewAfterTuningChange` already calls
`ShowZoomedView` when zoomed, and the mode-specific cache key selects or
generates the right output.

- [ ] **Step 6: Run tuning and config tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~TuningPanelWiringTests|FullyQualifiedName~TuningReloadValidationTests|FullyQualifiedName~VisualConfigServiceTests|FullyQualifiedName~TuningReapplyTests" --no-restore
```

Expected: Apply/Save/Reload mappings pass, the View still has no Services or
Utilities dependency, and a mode change uses the existing zoom replay path.

- [ ] **Step 7: Commit the Runtime Tuning slice**

```powershell
git add Models\TuningPanelEventArgs.cs Views\DeveloperTuningPanel.xaml Views\DeveloperTuningPanel.xaml.cs MainWindow.DeveloperTuning.partial.cs Tests\TuningPanelWiringTests.cs Tests\TuningReloadValidationTests.cs
git commit -m "feat: tune settled zoom resampling"
```

---

### Task 7: Add a repeatable visual comparison generator

**Files:**

- Create: `Tools/MapResamplerComparison/MapResamplerComparison.csproj`
- Create: `Tools/MapResamplerComparison/Program.cs`
- Modify: `InteractiveWorldMap.sln`
- Modify: `scripts/README.md`

- [ ] **Step 1: Create the tool project**

Use:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <LangVersion>10.0</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\InteractiveWorldMap.csproj" />
  </ItemGroup>
</Project>
```

Add it to the solution using:

```powershell
dotnet sln InteractiveWorldMap.sln add Tools\MapResamplerComparison\MapResamplerComparison.csproj
```

- [ ] **Step 2: Implement deterministic comparison output**

`Program.cs` accepts:

```text
--source <path> --crop <x,y,width,height> --output <directory>
```

Defaults are:

- source: `Images&Content\World Map 1976.jpg`;
- crop: `5160,7390,358,202`, which contains the `U` in `SOUTH`, black label
  strokes, blue borders/grid lines, colored relief edges, and smaller place
  text at approximately the shipped 55x crop size;
- output: `temp\map-resampler-comparison`.

For each of `1920x1080`, `2560x1440`, and `3840x2160`, generate one PNG per
enum mode with filenames such as:

```text
2560x1440_Fant.png
2560x1440_Lanczos3.png
2560x1440_MitchellNetravali.png
2560x1440_Bicubic.png
2560x1440_BicubicSharpened.png
```

Use `Stopwatch` around `ZoomedMapResampler.Resize`, save with
`PngBitmapEncoder`, and write `comparison.csv` with:

```text
width,height,mode,elapsed_ms,source_x,source_y,source_width,source_height,file
```

Exit nonzero with a concise error when the source/crop is invalid. Do not write
generated PNGs outside the ignored `temp/` default unless the caller explicitly
passes `--output`.

- [ ] **Step 3: Document the command and inspection criteria**

Add to `scripts/README.md`:

```powershell
dotnet run --project Tools\MapResamplerComparison\MapResamplerComparison.csproj -- `
  --source "Images&Content\World Map 1976.jpg" `
  --crop "5160,7390,358,202" `
  --output "temp\map-resampler-comparison"
```

Document comparison of the `SOUTH AMERICA` diagonals/interiors, thin borders,
edge-transition width, blockiness, halos, and elapsed time. State that generated
files are evidence, not committed product assets.

- [ ] **Step 4: Build and run the tool**

Run:

```powershell
dotnet build Tools\MapResamplerComparison\MapResamplerComparison.csproj --no-restore
dotnet run --project Tools\MapResamplerComparison\MapResamplerComparison.csproj --no-build
```

Expected: 15 PNGs plus `comparison.csv` under
`temp\map-resampler-comparison`; every PNG has the requested dimensions.

- [ ] **Step 5: Commit the comparison tooling**

```powershell
git add Tools\MapResamplerComparison InteractiveWorldMap.sln scripts\README.md
git commit -m "feat: generate zoom resampler comparisons"
```

---

### Task 8: Verify, compare live, and finish bookkeeping

**Files:**

- Modify: `docs/exec-plans/active/zoom-performance-appearance-plan.md`
- Modify: `docs/TO_DO.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/plans/2026-07-01-zoomed-map-upscaling.md`

- [ ] **Step 1: Run all focused rendering tests**

Run:

```powershell
dotnet test Tests\InteractiveWorldMap.Tests.csproj --filter "FullyQualifiedName~ZoomedMap|FullyQualifiedName~ZoomedRegion|FullyQualifiedName~PhysicalPixelSize|FullyQualifiedName~MapImageRenderingPolicy|FullyQualifiedName~TuningPanelWiring|FullyQualifiedName~TuningReloadValidation|FullyQualifiedName~ViewportState|FullyQualifiedName~MarkerPlacementOrchestrator" --no-restore
```

Expected: zero failed tests.

- [ ] **Step 2: Run the repository completion gate**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: build, complete test suite, vulnerability scan, seed verification,
doc links, taste checks, and headless startup all pass.

- [ ] **Step 3: Perform the live Windows comparison**

At the target full-screen size:

1. Zoom to the cluster whose crop includes the `SOUTH AMERICA` label.
2. Select each Runtime Tuning mode and Apply.
3. Confirm the settled bitmap fills the same bounds and markers/hit targets do
   not move.
4. Compare the letter interiors/diagonals and thin country/grid/coastline
   strokes for blockiness, softness, ringing, halos, and deformation.
5. Repeat once after returning to the full map to verify cache-hit output.
6. Repeat after moving the app to any available monitor with a different DPI.
7. Record first-generation and cache-hit times from logs/tool CSV.
8. Record the preferred shipping mode; keep `Fant` if no alternative is a clear
   improvement.

Do not mark visual acceptance complete if GUI control is unavailable. Record
the exact blocker and leave only that verification scope active.

- [ ] **Step 4: Update the owning active plan**

Under item 2.7 in
`docs/exec-plans/active/zoom-performance-appearance-plan.md`, record:

- physical-pixel settled rendering status;
- cache-identity status and schema version 8;
- implemented comparison modes;
- focused/full verification results;
- comparison output location and selected default;
- any remaining live visual verification.

Do not archive the owning plan because its unrelated Phase 2 work remains.

- [ ] **Step 5: Narrow completion state in TO_DO**

If code and live comparison are complete, remove the high-priority settled
zoom-rendering bullet. Retain the four Deferred bullets for lower/native zoom,
higher-resolution/vector source, vector overlays, and offline neural
super-resolution.

If only live comparison remains, narrow the high-priority bullet to that exact
manual comparison and default-selection scope; do not leave implementation
work worded as incomplete.

- [ ] **Step 6: Update the changelog and this plan**

Replace the design-only `[Unreleased]` entry with shipped behavior:

```markdown
- **Zoomed-map rendering options:** Settled zoom output is generated at physical monitor pixels and cached by source fingerprint, DPI, output size, resampler, and policy version. Runtime Tuning can compare Fant, Lanczos3, Mitchell-Netravali, bicubic, and restrained sharpened bicubic while animation remains Linear.
```

Add verification/default-selection detail if live comparison completed. Mark
completed checkboxes in this plan; leave a precise unchecked live step if GUI
verification is blocked.

- [ ] **Step 7: Re-run documentation checks**

Run:

```powershell
py -3 scripts\verify_doc_links.py
py -3 scripts\doc_gardening.py
git diff --check
```

Expected: both documentation scripts pass and `git diff --check` is empty.

- [ ] **Step 8: Commit finish bookkeeping**

```powershell
git add docs\exec-plans\active\zoom-performance-appearance-plan.md docs\TO_DO.md CHANGELOG.md docs\superpowers\plans\2026-07-01-zoomed-map-upscaling.md
git commit -m "docs: record zoomed map rendering rollout"
```

- [ ] **Step 9: Confirm final repository state**

Run:

```powershell
git status --short
git log -8 --oneline
```

Expected: clean worktree and a reviewable sequence of config, DPI, resampler,
cache, wiring, tuning, tooling, and bookkeeping commits.
