using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Utilities
{
    /// <summary>
    /// Clusters locations based on Euclidean distance in pixel coordinates.
    /// </summary>
    public class LocationClusterer
    {
        /// <summary>
        /// Distance threshold in pixels for clustering locations together.
        /// Default is 300 pixels.
        /// </summary>
        public double DistanceThreshold { get; set; } = 300.0;

        /// <summary>
        /// Clusters locations using simple Euclidean distance-based algorithm.
        /// </summary>
        /// <param name="locations">List of locations to cluster</param>
        /// <returns>List of clusters</returns>
        public List<LocationCluster> ClusterLocations(List<Location> locations)
        {
            if (locations == null || locations.Count == 0)
                return new List<LocationCluster>();

            var clusters = new List<LocationCluster>();
            var processed = new HashSet<string>();
            int clusterIndex = 0;

            foreach (var location in locations)
            {
                // Skip if already processed
                if (processed.Contains(location.Id))
                    continue;

                // Find all locations within threshold distance
                var nearbyLocations = FindNearbyLocations(location, locations, processed);
                
                // Create cluster
                var cluster = new LocationCluster
                {
                    Id = $"cluster_{clusterIndex++:D3}",
                    Locations = nearbyLocations
                };

                // Calculate center point
                cluster.CenterPoint = CalculateCenterPoint(nearbyLocations);

                clusters.Add(cluster);

                // Mark all locations in this cluster as processed
                foreach (var loc in nearbyLocations)
                {
                    processed.Add(loc.Id);
                }
            }

            return clusters;
        }

        /// <summary>
        /// Finds all locations within the distance threshold of the seed location.
        /// Uses recursive search to find all connected locations.
        /// </summary>
        private List<Location> FindNearbyLocations(
            Location seedLocation, 
            List<Location> allLocations, 
            HashSet<string> processed)
        {
            var cluster = new List<Location> { seedLocation };
            var toProcess = new Queue<Location>();
            toProcess.Enqueue(seedLocation);
            var inCluster = new HashSet<string> { seedLocation.Id };

            while (toProcess.Count > 0)
            {
                var current = toProcess.Dequeue();

                // Find all unprocessed locations within threshold
                foreach (var location in allLocations)
                {
                    if (processed.Contains(location.Id) || inCluster.Contains(location.Id))
                        continue;

                    double distance = CalculateDistance(current, location);
                    
                    if (distance <= DistanceThreshold)
                    {
                        cluster.Add(location);
                        inCluster.Add(location.Id);
                        toProcess.Enqueue(location);
                    }
                }
            }

            return cluster;
        }

        /// <summary>
        /// Calculates Euclidean distance between two locations in pixel coordinates.
        /// </summary>
        private double CalculateDistance(Location loc1, Location loc2)
        {
            double dx = loc1.PixelX - loc2.PixelX;
            double dy = loc1.PixelY - loc2.PixelY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculates the geometric center point of a list of locations.
        /// </summary>
        private Point CalculateCenterPoint(List<Location> locations)
        {
            if (locations.Count == 0)
                return new Point(0, 0);

            double sumX = 0;
            double sumY = 0;

            foreach (var location in locations)
            {
                sumX += location.PixelX;
                sumY += location.PixelY;
            }

            return new Point(
                sumX / locations.Count,
                sumY / locations.Count
            );
        }

        /// <summary>
        /// Gets statistics about the clustering results.
        /// </summary>
        public ClusteringStats GetClusteringStats(List<LocationCluster> clusters)
        {
            return new ClusteringStats
            {
                TotalClusters = clusters.Count,
                SingleLocationClusters = clusters.Count(c => c.IsSingleLocation),
                MultiLocationClusters = clusters.Count(c => !c.IsSingleLocation),
                LargestClusterSize = clusters.Any() ? clusters.Max(c => c.Count) : 0,
                TotalLocations = clusters.Sum(c => c.Count)
            };
        }
    }

    /// <summary>
    /// Statistics about clustering results.
    /// </summary>
    public class ClusteringStats
    {
        public int TotalClusters { get; set; }
        public int SingleLocationClusters { get; set; }
        public int MultiLocationClusters { get; set; }
        public int LargestClusterSize { get; set; }
        public int TotalLocations { get; set; }
    }
}
