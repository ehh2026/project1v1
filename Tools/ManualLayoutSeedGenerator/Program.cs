using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;

namespace InteractiveWorldMap.Tools.ManualLayoutSeedGenerator;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var parsed = CliOptions.Parse(args);
            var logger = new ConsoleLogger();
            var config = new VisualConfigService().Load(ResolveConfigPath(parsed.ConfigPath));
            var locations = new ExcelCoordinateReader(logger).ReadLocationsFromExcel(parsed.ExcelPath);
            var (imageWidth, imageHeight) = ReadImageDimensions(parsed.MapImagePath);
            var existing = LoadExisting(parsed.OutputPath);

            var generator = new ManualLayoutSeedGenerator(logger);
            var collection = generator.Generate(new ManualLayoutSeedGeneratorOptions
            {
                Config = config,
                Locations = locations,
                MapImageWidth = imageWidth,
                MapImageHeight = imageHeight,
                ExistingCollection = existing
            });

            var directory = Path.GetDirectoryName(parsed.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(parsed.OutputPath, JsonSerializer.Serialize(collection, CreateJsonOptions()));
            logger.LogInfo($"[ManualLayoutSeedGenerator] Wrote {parsed.OutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    // A fresh checkout has no user visual-config.json (it is seeded at app runtime), so fall
    // back to the tracked visual-config.default.json when the requested file is absent.
    private static string ResolveConfigPath(string requested)
    {
        if (File.Exists(requested))
            return requested;

        var fallback = Path.Combine(
            Path.GetDirectoryName(requested) ?? string.Empty,
            "visual-config.default.json");
        return File.Exists(fallback) ? fallback : requested;
    }

    private static ManualLayoutCollection LoadExisting(string outputPath)
    {
        if (!File.Exists(outputPath))
            return new ManualLayoutCollection();

        var json = File.ReadAllText(outputPath);
        return JsonSerializer.Deserialize<ManualLayoutCollection>(json, CreateJsonOptions()) ?? new ManualLayoutCollection();
    }

    private static (double Width, double Height) ReadImageDimensions(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Map image file not found.", imagePath);

        using var stream = File.OpenRead(imagePath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class ConsoleLogger : ILogger
    {
        public void LogInfo(string message) => Console.WriteLine(message);
        public void LogWarning(string message) => Console.WriteLine("WARNING: " + message);
        public void LogError(string message, Exception? ex = null)
        {
            Console.Error.WriteLine("ERROR: " + message);
            if (ex != null)
                Console.Error.WriteLine(ex);
        }
    }

    private sealed record CliOptions
    {
        public string ConfigPath { get; private init; } = "visual-config.json";
        public string ExcelPath { get; private init; } = "Coordinates for map.xlsx";
        public string MapImagePath { get; private init; } = Path.Combine("Images&Content", "World Map Extra Large.jpg");
        public string OutputPath { get; private init; } = Path.Combine("Images&Content", "manual-layouts.json");

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            for (var i = 0; i < args.Length; i++)
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value for argument '{args[i]}'.");

                var value = args[++i];
                options = args[i - 1] switch
                {
                    "--config" => options with { ConfigPath = value },
                    "--excel" => options with { ExcelPath = value },
                    "--map-image" => options with { MapImagePath = value },
                    "--output" => options with { OutputPath = value },
                    _ => throw new ArgumentException($"Unknown argument '{args[i - 1]}'.")
                };
            }

            return options;
        }
    }
}
