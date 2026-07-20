// PinDebugger CLI context: parsed args, paths, and active mode flags.
// Inputs: command-line args. Outputs: PinDebuggerContext for mode dispatch.

/// <summary>Resolved CLI configuration for a PinDebugger run.</summary>
internal sealed record PinDebuggerContext(
    bool CleanMode,
    bool FindJoinMode,
    bool FitAxisMode,
    bool MeasureShaftMode,
    bool CompositesMode,
    bool UseLitShafts,
    string PartsDir,
    string CleanedDir,
    string OutputDir,
    string ModeName)
{
    internal static PinDebuggerContext FromArgs(string[] args)
    {
        bool cleanMode        = args.Any(a => a == "--clean");
        bool findJoinMode     = args.Any(a => a == "--find-join");
        bool fitAxisMode      = args.Any(a => a == "--fit-axis");
        bool measureShaftMode = args.Any(a => a == "--measure-shaft");
        bool compositesMode   = args.Any(a => a == "--composites");
        bool useLitShafts     = args.Any(a => a == "--lit");
        var  posArgs          = args.Where(a => !a.StartsWith("--")).ToArray();

        var partsDir   = posArgs.Length > 0 ? posArgs[0] : Path.Combine("Images&Content", "Assets", "Pins_v2", "parts");
        var cleanedDir = Path.Combine("Tools", "PinDebugger", "cleaned");
        var outputDir  = posArgs.Length > 1 ? posArgs[1]
            : cleanMode        ? cleanedDir
            : findJoinMode     ? Path.Combine("Tools", "PinDebugger", "find-join")
            : fitAxisMode      ? Path.Combine("Tools", "PinDebugger", "find-join")
            : measureShaftMode ? Path.Combine("Tools", "PinDebugger", "find-join")
            : compositesMode   ? Path.Combine("Tools", "PinDebugger", "composites")
            : Path.Combine("Tools", "PinDebugger", "output_v2");

        Directory.CreateDirectory(outputDir);

        string modeName = cleanMode ? "clean" : findJoinMode ? "find-join" : fitAxisMode ? "fit-axis"
                        : measureShaftMode ? "measure-shaft"
                        : compositesMode ? $"composites{(useLitShafts ? " --lit" : "")}" : "annotate";

        return new PinDebuggerContext(
            cleanMode, findJoinMode, fitAxisMode, measureShaftMode, compositesMode, useLitShafts,
            partsDir, cleanedDir, outputDir, modeName);
    }

    internal void PrintBanner()
    {
        Console.WriteLine($"Mode   : {ModeName}");
        Console.WriteLine($"Parts  : {PartsDir}");
        Console.WriteLine($"Output : {OutputDir}");
        Console.WriteLine();
    }
}
