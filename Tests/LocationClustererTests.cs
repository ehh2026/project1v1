using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public void ClusterLocations_NullList_ReturnsEmptyClusters()
        {
            var clusterer = new LocationClusterer();
            var clusters = clusterer.ClusterLocations(null!);
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
            Assert.Equal(new[] { "loc1", "loc2" }, clusters[0].Locations.Select(l => l.Id).OrderBy(id => id));
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
            Assert.Equal(
                new HashSet<string> { "loc1", "loc2", "loc3" },
                clusters[0].Locations.Select(l => l.Id).ToHashSet());
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

            var membership = clusters
                .Select(c => c.Locations.Select(l => l.Id).OrderBy(id => id).ToArray())
                .OrderBy(ids => ids[0])
                .ToList();
            Assert.Equal(new[] { "loc1", "loc2" }, membership[0]);
            Assert.Equal(new[] { "loc3", "loc4" }, membership[1]);
            Assert.Equal(new[] { "loc5" }, membership[2]);
        }

        [Fact]
        public void ClusterLocations_PointsAcrossCellBoundaryWithinThreshold_StillCluster()
        {
            // Threshold 100 → cell size 100. Points near opposite edges of adjacent cells
            // (99, 50) and (101, 50) are ~2px apart and must share a cluster via 3×3 scan.
            var clusterer = new LocationClusterer { DistanceThreshold = 100 };
            var locations = new List<Location>
            {
                new Location { Id = "left", PixelX = 99, PixelY = 50 },
                new Location { Id = "right", PixelX = 101, PixelY = 50 }
            };

            var clusters = clusterer.ClusterLocations(locations);

            Assert.Single(clusters);
            Assert.Equal(
                new HashSet<string> { "left", "right" },
                clusters[0].Locations.Select(l => l.Id).ToHashSet());
        }

        [Fact]
        public void ClusterLocations_DiagonalCellNeighborsWithinThreshold_StillCluster()
        {
            // Cells (0,0) and (1,1) with points near the shared corner; distance ~14 < 100.
            var clusterer = new LocationClusterer { DistanceThreshold = 100 };
            var locations = new List<Location>
            {
                new Location { Id = "a", PixelX = 95, PixelY = 95 },
                new Location { Id = "b", PixelX = 105, PixelY = 105 }
            };

            var clusters = clusterer.ClusterLocations(locations);

            Assert.Single(clusters);
            Assert.Equal(2, clusters[0].Count);
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

        /// <summary>
        /// Documents spatial-index scaling for n≥200. Soft timing ceiling only.
        /// Marked Category=Performance and excluded from default verify/CI via
        /// <c>--filter "Category!=Performance"</c>; run manually when measuring.
        /// </summary>
        [Fact]
        [Trait("Category", "Performance")]
        public void ClusterLocations_LargeSyntheticSet_CompletesQuickly()
        {
            const int n = 250;
            const double threshold = 50;
            var clusterer = new LocationClusterer { DistanceThreshold = threshold };
            var locations = new List<Location>(n);
            var rng = new Random(42);
            for (var i = 0; i < n; i++)
            {
                locations.Add(new Location
                {
                    Id = $"loc_{i:D4}",
                    PixelX = rng.NextDouble() * 5000,
                    PixelY = rng.NextDouble() * 5000
                });
            }

            var sw = Stopwatch.StartNew();
            var clusters = clusterer.ClusterLocations(locations);
            sw.Stop();

            Assert.Equal(n, clusters.Sum(c => c.Count));
            // Soft ceiling: grid neighbor scan should finish well under a second on CI hardware.
            Assert.True(
                sw.ElapsedMilliseconds < 2000,
                $"Clustering {n} points took {sw.ElapsedMilliseconds}ms (expected < 2000ms with spatial grid).");
        }
    }
}
