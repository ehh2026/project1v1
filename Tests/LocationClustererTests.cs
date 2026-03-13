using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Utilities;
using Xunit;

namespace InteractiveWorldMap.Tests
{
    public class LocationClustererTests
    {
        [Fact]
        public void ClusterLocations_EmptyList_ReturnsEmptyClusters()
        {
            // Arrange
            var clusterer = new LocationClusterer();
            var locations = new List<Location>();

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Empty(clusters);
        }

        [Fact]
        public void ClusterLocations_SingleLocation_ReturnsSingleCluster()
        {
            // Arrange
            var clusterer = new LocationClusterer();
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 100, PixelY = 100 }
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Single(clusters);
            Assert.Single(clusters[0].Locations);
            Assert.True(clusters[0].IsSingleLocation);
        }

        [Fact]
        public void ClusterLocations_TwoLocationsWithinThreshold_CreatesOneCluster()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 100, PixelY = 100 },
                new Location { Id = "loc2", PixelX = 200, PixelY = 100 } // 100 pixels apart
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Single(clusters);
            Assert.Equal(2, clusters[0].Count);
            Assert.False(clusters[0].IsSingleLocation);
        }

        [Fact]
        public void ClusterLocations_TwoLocationsBeyondThreshold_CreatesTwoClusters()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 100, PixelY = 100 },
                new Location { Id = "loc2", PixelX = 500, PixelY = 100 } // 400 pixels apart
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Equal(2, clusters.Count);
            Assert.All(clusters, c => Assert.Single(c.Locations));
        }

        [Fact]
        public void ClusterLocations_ExactlyAtThreshold_CreatesOneCluster()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 0, PixelY = 0 },
                new Location { Id = "loc2", PixelX = 300, PixelY = 0 } // Exactly 300 pixels
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Single(clusters);
            Assert.Equal(2, clusters[0].Count);
        }

        [Fact]
        public void ClusterLocations_DiagonalDistance_CalculatesCorrectly()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 0, PixelY = 0 },
                new Location { Id = "loc2", PixelX = 200, PixelY = 200 } // ~283 pixels (within threshold)
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Single(clusters);
            Assert.Equal(2, clusters[0].Count);
        }

        [Fact]
        public void ClusterLocations_ThreeLocationsInChain_CreatesOneCluster()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 0, PixelY = 0 },
                new Location { Id = "loc2", PixelX = 250, PixelY = 0 },   // 250px from loc1
                new Location { Id = "loc3", PixelX = 500, PixelY = 0 }    // 250px from loc2, 500px from loc1
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            // All three should be in one cluster due to transitive connectivity
            Assert.Single(clusters);
            Assert.Equal(3, clusters[0].Count);
        }

        [Fact]
        public void ClusterLocations_MultipleGroups_CreatesMultipleClusters()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                // Group 1
                new Location { Id = "loc1", PixelX = 100, PixelY = 100 },
                new Location { Id = "loc2", PixelX = 200, PixelY = 100 },
                
                // Group 2
                new Location { Id = "loc3", PixelX = 1000, PixelY = 1000 },
                new Location { Id = "loc4", PixelX = 1100, PixelY = 1000 },
                
                // Isolated
                new Location { Id = "loc5", PixelX = 5000, PixelY = 5000 }
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Equal(3, clusters.Count);
            Assert.Equal(2, clusters.Count(c => c.Count == 2));
            Assert.Single(clusters.Where(c => c.Count == 1));
        }

        [Fact]
        public void ClusterLocations_CenterPoint_CalculatesAverage()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 0, PixelY = 0 },
                new Location { Id = "loc2", PixelX = 100, PixelY = 0 },
                new Location { Id = "loc3", PixelX = 200, PixelY = 0 }
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            Assert.Single(clusters);
            Assert.Equal(100, clusters[0].CenterPoint.X); // Average of 0, 100, 200
            Assert.Equal(0, clusters[0].CenterPoint.Y);
        }

        [Fact]
        public void GetClusteringStats_ReturnsCorrectStatistics()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 300 };
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 100, PixelY = 100 },
                new Location { Id = "loc2", PixelX = 200, PixelY = 100 },
                new Location { Id = "loc3", PixelX = 250, PixelY = 100 },
                new Location { Id = "loc4", PixelX = 1000, PixelY = 1000 }
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);
            var stats = clusterer.GetClusteringStats(clusters);

            // Assert
            Assert.Equal(2, stats.TotalClusters);
            Assert.Equal(1, stats.SingleLocationClusters);
            Assert.Equal(1, stats.MultiLocationClusters);
            Assert.Equal(3, stats.LargestClusterSize);
            Assert.Equal(4, stats.TotalLocations);
        }

        [Fact]
        public void ClusterLocations_CustomThreshold_UsesSpecifiedValue()
        {
            // Arrange
            var clusterer = new LocationClusterer { DistanceThreshold = 100 }; // Smaller threshold
            var locations = new List<Location>
            {
                new Location { Id = "loc1", PixelX = 0, PixelY = 0 },
                new Location { Id = "loc2", PixelX = 150, PixelY = 0 } // 150 pixels apart
            };

            // Act
            var clusters = clusterer.ClusterLocations(locations);

            // Assert
            // Should create two clusters with smaller threshold
            Assert.Equal(2, clusters.Count);
        }
    }
}
