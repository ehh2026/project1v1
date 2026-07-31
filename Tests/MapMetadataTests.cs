using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for <see cref="MapMetadata"/> construction, bitmap loading, and display ceilings.
/// </summary>
public class MapMetadataTests
{
    [Fact]
    public void CreateDefault_MatchesDocumentedAssetDimensions()
    {
        var meta = MapMetadata.CreateDefault();

        Assert.Equal(MapMetadata.DefaultDisplayWidth, meta.DisplayWidth);
        Assert.Equal(MapMetadata.DefaultDisplayHeight, meta.DisplayHeight);
        Assert.Equal(MapMetadata.DefaultFullResWidth, meta.FullResWidth);
        Assert.Equal(MapMetadata.DefaultFullResHeight, meta.FullResHeight);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDisplaySize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapMetadata(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapMetadata(100, -1));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveFullResWhenSet()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapMetadata(10, 10, fullResWidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapMetadata(10, 10, fullResHeight: -5));
    }

    [Fact]
    public void FromDisplayBitmap_UsesPixelDimensions_PreservesFullResFromDefaults()
    {
        var bitmap = CreateBitmap(100, 50);
        var meta = MapMetadata.FromDisplayBitmap(bitmap);

        Assert.Equal(100, meta.DisplayWidth);
        Assert.Equal(50, meta.DisplayHeight);
        Assert.Equal(MapMetadata.DefaultFullResWidth, meta.FullResWidth);
        Assert.Equal(MapMetadata.DefaultFullResHeight, meta.FullResHeight);
    }

    [Fact]
    public void FromDisplayBitmap_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MapMetadata.FromDisplayBitmap(null!));
    }

    [Fact]
    public void IsValidDisplayCoordinate_InclusiveDisplayBounds()
    {
        var meta = new MapMetadata(8198, 5542);

        Assert.True(meta.IsValidDisplayCoordinate(0, 0));
        Assert.True(meta.IsValidDisplayCoordinate(8198, 5542));
        Assert.False(meta.IsValidDisplayCoordinate(-0.1, 10));
        Assert.False(meta.IsValidDisplayCoordinate(10, 5542.1));
        // Full-res values are outside display ceilings.
        Assert.False(meta.IsValidDisplayCoordinate(16397, 11085));
    }

    private static BitmapSource CreateBitmap(int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}
