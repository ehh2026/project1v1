using System.Windows;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CoordinateMapperTests
{
    [Fact]
    public void LatLongToScreen_EquatorPrimeMeridian_ReturnsCenterPoint()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };

        // Act - Equator (0°) and Prime Meridian (0°) should map to center
        var result = mapper.LatLongToScreen(0, 0);

        // Assert
        Assert.Equal(500, result.X, 1); // Center X
        Assert.Equal(250, result.Y, 1); // Center Y
    }

    [Fact]
    public void LatLongToScreen_NorthPole_ReturnsTopCenter()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };

        // Act - North Pole (90°, 0°) should map to top center
        var result = mapper.LatLongToScreen(90, 0);

        // Assert
        Assert.Equal(500, result.X, 1); // Center X
        Assert.Equal(0, result.Y, 1);   // Top Y
    }

    [Fact]
    public void LatLongToScreen_SouthPole_ReturnsBottomCenter()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };

        // Act - South Pole (-90°, 0°) should map to bottom center
        var result = mapper.LatLongToScreen(-90, 0);

        // Assert
        Assert.Equal(500, result.X, 1);  // Center X
        Assert.Equal(500, result.Y, 1);  // Bottom Y
    }

    [Fact]
    public void LatLongToScreen_DateLine_ReturnsRightEdge()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };

        // Act - International Date Line (0°, 180°) should map to right edge
        var result = mapper.LatLongToScreen(0, 180);

        // Assert
        Assert.Equal(1000, result.X, 1); // Right edge X
        Assert.Equal(250, result.Y, 1);  // Center Y
    }

    [Fact]
    public void LatLongToScreen_WesternHemisphere_ReturnsLeftSide()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };

        // Act - Western hemisphere (0°, -180°) should map to left edge
        var result = mapper.LatLongToScreen(0, -180);

        // Assert
        Assert.Equal(0, result.X, 1);    // Left edge X
        Assert.Equal(250, result.Y, 1);  // Center Y
    }

    [Fact]
    public void ScreenToLatLong_CenterPoint_ReturnsEquatorPrimeMeridian()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };

        // Act - Center point should map to Equator and Prime Meridian
        var (lat, lon) = mapper.ScreenToLatLong(new Point(500, 250));

        // Assert
        Assert.Equal(0, lat, 1);  // Equator
        Assert.Equal(0, lon, 1);  // Prime Meridian
    }

    [Fact]
    public void ScreenToLatLong_TopCenter_ReturnsNorthPole()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };

        // Act - Top center should map to North Pole
        var (lat, lon) = mapper.ScreenToLatLong(new Point(500, 0));

        // Assert
        Assert.Equal(90, lat, 1);  // North Pole
        Assert.Equal(0, lon, 1);   // Prime Meridian
    }

    [Fact]
    public void RoundTrip_PreservesCoordinates()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1920, 1080),
            ScreenSize = new Size(1920, 1080)
        };
        var originalLat = 40.7128;  // New York
        var originalLon = -74.0060;

        // Act - Convert to screen and back
        var screenPoint = mapper.LatLongToScreen(originalLat, originalLon);
        var (resultLat, resultLon) = mapper.ScreenToLatLong(screenPoint);

        // Assert - Should preserve coordinates within tolerance
        Assert.Equal(originalLat, resultLat, 2);
        Assert.Equal(originalLon, resultLon, 2);
    }

    [Fact]
    public void UpdateProjection_UpdatesMapBounds()
    {
        // Arrange
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1000, 500),
            ScreenSize = new Size(1000, 500)
        };
        var newBounds = new Rect(100, 100, 800, 400);

        // Act
        mapper.UpdateProjection(newBounds);

        // Assert
        Assert.Equal(newBounds, mapper.MapBounds);
    }

    [Fact]
    public void LatLongToScreen_WithOffset_AccountsForMapBounds()
    {
        // Arrange - Map is offset from screen origin
        var mapper = new CoordinateMapper
        {
            MapBounds = new Rect(100, 50, 1000, 500),
            ScreenSize = new Size(1200, 600)
        };

        // Act - Equator and Prime Meridian
        var result = mapper.LatLongToScreen(0, 0);

        // Assert - Should account for offset
        Assert.Equal(600, result.X, 1);  // 100 + 500
        Assert.Equal(300, result.Y, 1);  // 50 + 250
    }

    [Fact]
    public void LatLongToScreen_DifferentResolutions_MaintainsRelativePosition()
    {
        // Arrange - Test at different screen resolutions
        var mapper1 = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 1920, 1080),
            ScreenSize = new Size(1920, 1080)
        };
        var mapper2 = new CoordinateMapper
        {
            MapBounds = new Rect(0, 0, 3840, 2160),
            ScreenSize = new Size(3840, 2160)
        };
        var testLat = 51.5074;  // London
        var testLon = -0.1278;

        // Act
        var point1 = mapper1.LatLongToScreen(testLat, testLon);
        var point2 = mapper2.LatLongToScreen(testLat, testLon);

        // Assert - Relative positions should be the same (2x scaling)
        Assert.Equal(point1.X * 2, point2.X, 1);
        Assert.Equal(point1.Y * 2, point2.Y, 1);
    }
}
