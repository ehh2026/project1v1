using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CoordinateValidatorTests
{
    private readonly CoordinateValidator _validator = new();

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(90, 180, true)]
    [InlineData(-90, -180, true)]
    [InlineData(45.5, 123.456, true)]
    [InlineData(-45.5, -123.456, true)]
    public void IsValid_ValidCoordinates_ReturnsTrue(double lat, double lon, bool expected)
    {
        // Act
        var result = _validator.IsValid(lat, lon);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(91, 0, false)]
    [InlineData(-91, 0, false)]
    [InlineData(0, 181, false)]
    [InlineData(0, -181, false)]
    [InlineData(100, 200, false)]
    [InlineData(-100, -200, false)]
    public void IsValid_InvalidCoordinates_ReturnsFalse(double lat, double lon, bool expected)
    {
        // Act
        var result = _validator.IsValid(lat, lon);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsValid_NorthPole_ReturnsTrue()
    {
        // Act
        var result = _validator.IsValid(90, 0);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_SouthPole_ReturnsTrue()
    {
        // Act
        var result = _validator.IsValid(-90, 0);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_DateLine_ReturnsTrue()
    {
        // Act - Test both sides of the International Date Line
        var resultEast = _validator.IsValid(0, 180);
        var resultWest = _validator.IsValid(0, -180);

        // Assert
        Assert.True(resultEast);
        Assert.True(resultWest);
    }

    [Fact]
    public void Clamp_ValidCoordinates_ReturnsUnchanged()
    {
        // Arrange
        var lat = 45.0;
        var lon = 90.0;

        // Act
        var (clampedLat, clampedLon) = _validator.Clamp(lat, lon);

        // Assert
        Assert.Equal(lat, clampedLat);
        Assert.Equal(lon, clampedLon);
    }

    [Fact]
    public void Clamp_LatitudeTooHigh_ClampsTo90()
    {
        // Arrange
        var lat = 100.0;
        var lon = 0.0;

        // Act
        var (clampedLat, clampedLon) = _validator.Clamp(lat, lon);

        // Assert
        Assert.Equal(90.0, clampedLat);
        Assert.Equal(lon, clampedLon);
    }

    [Fact]
    public void Clamp_LatitudeTooLow_ClampsToNegative90()
    {
        // Arrange
        var lat = -100.0;
        var lon = 0.0;

        // Act
        var (clampedLat, clampedLon) = _validator.Clamp(lat, lon);

        // Assert
        Assert.Equal(-90.0, clampedLat);
        Assert.Equal(lon, clampedLon);
    }

    [Fact]
    public void Clamp_LongitudeTooHigh_ClampsTo180()
    {
        // Arrange
        var lat = 0.0;
        var lon = 200.0;

        // Act
        var (clampedLat, clampedLon) = _validator.Clamp(lat, lon);

        // Assert
        Assert.Equal(lat, clampedLat);
        Assert.Equal(180.0, clampedLon);
    }

    [Fact]
    public void Clamp_LongitudeTooLow_ClampsToNegative180()
    {
        // Arrange
        var lat = 0.0;
        var lon = -200.0;

        // Act
        var (clampedLat, clampedLon) = _validator.Clamp(lat, lon);

        // Assert
        Assert.Equal(lat, clampedLat);
        Assert.Equal(-180.0, clampedLon);
    }

    [Fact]
    public void Clamp_BothOutOfRange_ClampsBoth()
    {
        // Arrange
        var lat = 150.0;
        var lon = -250.0;

        // Act
        var (clampedLat, clampedLon) = _validator.Clamp(lat, lon);

        // Assert
        Assert.Equal(90.0, clampedLat);
        Assert.Equal(-180.0, clampedLon);
    }

    [Fact]
    public void Clamp_EdgeCases_HandlesCorrectly()
    {
        // Test exact boundary values
        var (lat90, lon180) = _validator.Clamp(90.0, 180.0);
        var (latNeg90, lonNeg180) = _validator.Clamp(-90.0, -180.0);

        Assert.Equal(90.0, lat90);
        Assert.Equal(180.0, lon180);
        Assert.Equal(-90.0, latNeg90);
        Assert.Equal(-180.0, lonNeg180);
    }
}
