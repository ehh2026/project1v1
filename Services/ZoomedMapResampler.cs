using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public sealed class ZoomedMapResampler : IZoomedMapResampler
{
    public const int PolicyVersion = 1;

    public BitmapSource Resize(BitmapSource source, int width, int height, ZoomedMapResamplingMode mode)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (mode == ZoomedMapResamplingMode.Fant) return Fant(source, width, height);

        Func<double, double> kernel;
        double radius;
        switch (mode)
        {
            case ZoomedMapResamplingMode.Lanczos3: kernel = Lanczos; radius = 3; break;
            case ZoomedMapResamplingMode.MitchellNetravali: kernel = Mitchell; radius = 2; break;
            case ZoomedMapResamplingMode.Bicubic:
            case ZoomedMapResamplingMode.BicubicSharpened: kernel = CatmullRom; radius = 2; break;
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var result = Separable(source, width, height, kernel, radius);
        return mode == ZoomedMapResamplingMode.BicubicSharpened ? Sharpen(result) : result;
    }

    private static BitmapSource Fant(BitmapSource source, int width, int height)
    {
        var scaled = new TransformedBitmap(source,
            new ScaleTransform(width / (double)source.PixelWidth, height / (double)source.PixelHeight));
        RenderOptions.SetBitmapScalingMode(scaled, BitmapScalingMode.Fant);
        var result = new WriteableBitmap(scaled);
        result.Freeze();
        return result;
    }

    private sealed record Weight(int Index, double Value);

    private static List<Weight>[] Contributions(int sourceLength, int destinationLength,
        Func<double, double> kernel, double radius)
    {
        var result = new List<Weight>[destinationLength];
        var scale = destinationLength / (double)sourceLength;
        var filterScale = Math.Min(1.0, scale);
        var support = radius / filterScale;
        for (var d = 0; d < destinationLength; d++)
        {
            var position = ((d + 0.5) / scale) - 0.5;
            var merged = new Dictionary<int, double>();
            var start = (int)Math.Ceiling(position - support);
            var end = (int)Math.Floor(position + support);
            for (var s = start; s <= end; s++)
            {
                var index = Math.Clamp(s, 0, sourceLength - 1);
                var weight = kernel((position - s) * filterScale) * filterScale;
                merged[index] = merged.TryGetValue(index, out var old) ? old + weight : weight;
            }
            var sum = 0.0;
            foreach (var value in merged.Values) sum += value;
            var list = new List<Weight>();
            if (Math.Abs(sum) < 1e-12)
                list.Add(new Weight(Math.Clamp((int)Math.Round(position), 0, sourceLength - 1), 1));
            else
                foreach (var pair in merged) list.Add(new Weight(pair.Key, pair.Value / sum));
            result[d] = list;
        }
        return result;
    }

    private static BitmapSource Separable(BitmapSource source, int width, int height,
        Func<double, double> kernel, double radius)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var sourceWidth = converted.PixelWidth;
        var sourceHeight = converted.PixelHeight;
        var input = new byte[sourceWidth * sourceHeight * 4];
        converted.CopyPixels(input, sourceWidth * 4, 0);
        var horizontal = new double[width * sourceHeight * 4];
        var xWeights = Contributions(sourceWidth, width, kernel, radius);
        Parallel.For(0, sourceHeight, y =>
        {
            for (var x = 0; x < width; x++)
                for (var c = 0; c < 4; c++)
                {
                    var value = 0.0;
                    foreach (var w in xWeights[x])
                        value += input[(y * sourceWidth + w.Index) * 4 + c] * w.Value;
                    horizontal[(y * width + x) * 4 + c] = value;
                }
        });

        var output = new byte[width * height * 4];
        var yWeights = Contributions(sourceHeight, height, kernel, radius);
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
                for (var c = 0; c < 4; c++)
                {
                    var value = 0.0;
                    foreach (var w in yWeights[y])
                        value += horizontal[(w.Index * width + x) * 4 + c] * w.Value;
                    output[(y * width + x) * 4 + c] =
                        (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
                }
        });
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, output, width * 4);
        result.Freeze();
        return result;
    }

    private static BitmapSource Sharpen(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var input = new byte[width * height * 4];
        source.CopyPixels(input, width * 4, 0);
        var output = (byte[])input.Clone();
        int[] weights = { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
                for (var c = 0; c < 3; c++)
                {
                    var sum = 0;
                    var k = 0;
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                            sum += input[(Math.Clamp(y + dy, 0, height - 1) * width +
                                          Math.Clamp(x + dx, 0, width - 1)) * 4 + c] * weights[k++];
                    var original = input[(y * width + x) * 4 + c];
                    var difference = original - (sum / 16.0);
                    var value = Math.Abs(difference) < 2 ? original : original + 0.25 * difference;
                    output[(y * width + x) * 4 + c] =
                        (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
                }
        });
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, output, width * 4);
        result.Freeze();
        return result;
    }

    private static double Sinc(double x) =>
        Math.Abs(x) < 1e-12 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);
    private static double Lanczos(double x) =>
        Math.Abs(x) < 3 ? Sinc(x) * Sinc(x / 3) : 0;
    private static double CatmullRom(double x)
    {
        const double a = -0.5;
        x = Math.Abs(x);
        if (x <= 1) return ((a + 2) * x - (a + 3)) * x * x + 1;
        return x < 2 ? (((a * x - 5 * a) * x + 8 * a) * x - 4 * a) : 0;
    }
    private static double Mitchell(double x)
    {
        const double b = 1.0 / 3.0, c = 1.0 / 3.0;
        x = Math.Abs(x);
        if (x < 1) return ((12 - 9 * b - 6 * c) * x * x * x + (-18 + 12 * b + 6 * c) * x * x + 6 - 2 * b) / 6;
        return x < 2 ? ((-b - 6 * c) * x * x * x + (6 * b + 30 * c) * x * x + (-12 * b - 48 * c) * x + 8 * b + 24 * c) / 6 : 0;
    }
}
