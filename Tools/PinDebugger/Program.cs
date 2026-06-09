// Pin geometry debug annotator + shadow cleaner + join finder.
// Run from the project root.
//
// Annotate mode (default):
//   dotnet run --project Tools\PinDebugger [partsDir [outputDir]]
//
// Clean mode:
//   dotnet run --project Tools\PinDebugger -- --clean [partsDir [cleanedDir]]
//
// Find-join mode:
//   dotnet run --project Tools\PinDebugger -- --find-join [partsDir [cleanedDir]]
//
// Fit-axis mode:
//   dotnet run --project Tools\PinDebugger -- --fit-axis [partsDir [cleanedDir]]
//
// Composites mode:
//   dotnet run --project Tools\PinDebugger -- --composites [partsDir [outputDir]]
//   Add --lit to use the _lit shaft variants.

using System.Text;
using System.Text.Json;

var ctx = PinDebuggerContext.FromArgs(args);
ctx.PrintBanner();

var raw  = File.ReadAllBytes(Path.Combine(ctx.PartsDir, "pin_part_geometry.json"));
int bom  = (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) ? 3 : 0;
var json = Encoding.UTF8.GetString(raw, bom, raw.Length - bom);
using var doc = JsonDocument.Parse(json);

if (ctx.CompositesMode)
{
    CompositePreviewRenderer.RunComposites(doc.RootElement, ctx.PartsDir, ctx.OutputDir, ctx.UseLitShafts);
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

        if (ctx.CleanMode)
        {
            Console.WriteLine($"  {pinId}");
            ShaftCleaner.CleanShaft(pin, pinId, ctx.PartsDir, ctx.OutputDir, litSuffix: false);
            ShaftCleaner.CleanShaft(pin, pinId, ctx.PartsDir, ctx.OutputDir, litSuffix: true);
        }
        else if (ctx.FindJoinMode)
        {
            JoinAnalysis.FindJoin(pin, pinId, ctx.PartsDir, ctx.CleanedDir);
        }
        else if (ctx.FitAxisMode)
        {
            JoinAnalysis.FitAxis(pin, pinId, ctx.PartsDir, ctx.CleanedDir);
        }
        else if (ctx.MeasureShaftMode)
        {
            JoinAnalysis.MeasureShaft(pin, pinId, ctx.PartsDir, ctx.CleanedDir);
        }
        else
        {
            Console.WriteLine($"  {pinId}");
            Annotator.AnnotateHead(pin, pinId, ctx.PartsDir, ctx.OutputDir);
            Annotator.AnnotateShaft(pin, pinId, ctx.PartsDir, ctx.OutputDir, litSuffix: false);
            Annotator.AnnotateShaft(pin, pinId, ctx.PartsDir, ctx.OutputDir, litSuffix: true);
        }
        count++;
    }

    Console.WriteLine();
    Console.WriteLine($"Done — {count} pins processed.");
}
