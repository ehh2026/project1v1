using System;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class ZoomedMapResamplerTests
{
    [Theory]
    [InlineData(ZoomedMapResamplingMode.Fant)]
    [InlineData(ZoomedMapResamplingMode.Lanczos3)]
    [InlineData(ZoomedMapResamplingMode.MitchellNetravali)]
    [InlineData(ZoomedMapResamplingMode.Bicubic)]
    [InlineData(ZoomedMapResamplingMode.BicubicSharpened)]
    public void Resize_AllModes_ReturnRequestedFrozenBitmap(ZoomedMapResamplingMode mode)
    {
        var result = new ZoomedMapResampler().Resize(CreateGradient(), 17, 11, mode);
        Assert.Equal(17, result.PixelWidth);
        Assert.Equal(11, result.PixelHeight);
        Assert.True(result.IsFrozen);
    }

    [Theory]
    [InlineData(ZoomedMapResamplingMode.Lanczos3)]
    [InlineData(ZoomedMapResamplingMode.MitchellNetravali)]
    [InlineData(ZoomedMapResamplingMode.Bicubic)]
    [InlineData(ZoomedMapResamplingMode.BicubicSharpened)]
    public void Resize_OnePixel_RemainsConstant(ZoomedMapResamplingMode mode)
    {
        var source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 17, 89, 201, 255 }, 4);
        var pixels = Read(new ZoomedMapResampler().Resize(source, 9, 7, mode));
        Assert.All(pixels.Chunk(4), p => Assert.Equal(new byte[] { 17, 89, 201, 255 }, p));
    }

    [Theory]
    [InlineData(ZoomedMapResamplingMode.Lanczos3)]
    [InlineData(ZoomedMapResamplingMode.MitchellNetravali)]
    [InlineData(ZoomedMapResamplingMode.Bicubic)]
    [InlineData(ZoomedMapResamplingMode.BicubicSharpened)]
    public void Resize_CustomModes_AreDeterministic(ZoomedMapResamplingMode mode)
    {
        var source = CreateGradient();
        Assert.Equal(
            Read(new ZoomedMapResampler().Resize(source, 13, 9, mode)),
            Read(new ZoomedMapResampler().Resize(source, 13, 9, mode)));
    }

    private static BitmapSource CreateGradient()
    {
        var pixels = Enumerable.Range(0, 4 * 4).SelectMany(i =>
            new byte[] { (byte)(i * 3), (byte)(i * 5), (byte)(i * 7), 255 }).ToArray();
        return BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
    }

    private static byte[] Read(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return pixels;
    }
}
