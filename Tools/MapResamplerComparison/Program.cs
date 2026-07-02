using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var sourcePath = Path.Combine(root, "Images&Content", "World Map 1976.jpg");
var outputPath = Path.Combine(root, "temp", "map-resampler-comparison");
var crop = new Int32Rect(5160, 7390, 358, 202);

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--source" && i + 1 < args.Length) sourcePath = Path.GetFullPath(args[++i]);
    else if (args[i] == "--output" && i + 1 < args.Length) outputPath = Path.GetFullPath(args[++i]);
    else if (args[i] == "--crop" && i + 1 < args.Length)
    {
        var values = args[++i].Split(',').Select(int.Parse).ToArray();
        if (values.Length != 4) throw new ArgumentException("--crop requires x,y,width,height");
        crop = new Int32Rect(values[0], values[1], values[2], values[3]);
    }
}

if (!File.Exists(sourcePath)) throw new FileNotFoundException("Map source not found.", sourcePath);
var bitmap = new BitmapImage();
bitmap.BeginInit();
bitmap.CacheOption = BitmapCacheOption.OnLoad;
bitmap.UriSource = new Uri(sourcePath);
bitmap.EndInit();
bitmap.Freeze();
if (crop.X < 0 || crop.Y < 0 || crop.Width <= 0 || crop.Height <= 0 ||
    crop.X + crop.Width > bitmap.PixelWidth || crop.Y + crop.Height > bitmap.PixelHeight)
    throw new ArgumentOutOfRangeException(nameof(crop), "Crop must fit inside the source image.");

Directory.CreateDirectory(outputPath);
var source = new CroppedBitmap(bitmap, crop);
var resampler = new ZoomedMapResampler();
using var csv = new StreamWriter(Path.Combine(outputPath, "comparison.csv"));
csv.WriteLine("width,height,mode,elapsed_ms,source_x,source_y,source_width,source_height,file");
foreach (var size in new[] { (1920, 1080), (2560, 1440), (3840, 2160) })
foreach (var mode in Enum.GetValues<ZoomedMapResamplingMode>())
{
    var watch = Stopwatch.StartNew();
    var result = resampler.Resize(source, size.Item1, size.Item2, mode);
    watch.Stop();
    var fileName = $"{size.Item1}x{size.Item2}_{mode}.png";
    using var stream = File.Create(Path.Combine(outputPath, fileName));
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(result));
    encoder.Save(stream);
    csv.WriteLine(string.Join(",", size.Item1, size.Item2, mode,
        watch.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture),
        crop.X, crop.Y, crop.Width, crop.Height, fileName));
    Console.WriteLine($"{fileName}: {watch.Elapsed.TotalMilliseconds:F0} ms");
}
