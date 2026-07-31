using System;
using System.Collections.Generic;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Utilities
{
    /// <summary>
    /// Uniform spatial hash for location neighbor queries in pixel space.
    /// Cell size is typically the clustering distance threshold; callers should
    /// query a 3×3 Moore neighborhood so Euclidean radius ≤ cell size is covered.
    /// </summary>
    public sealed class SpatialGrid
    {
        private readonly double _cellSize;
        private readonly Dictionary<(int CellX, int CellY), List<Location>> _cells = new();

        /// <summary>
        /// Creates a grid with the given cell size in pixels.
        /// </summary>
        /// <param name="cellSize">Positive cell edge length (usually the cluster distance threshold).</param>
        public SpatialGrid(double cellSize)
        {
            if (cellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be positive.");
            _cellSize = cellSize;
        }

        /// <summary>Cell edge length in pixels.</summary>
        public double CellSize => _cellSize;

        /// <summary>Number of occupied cells.</summary>
        public int OccupiedCellCount => _cells.Count;

        /// <summary>
        /// Inserts a location into the cell that contains its pixel coordinates.
        /// </summary>
        public void Insert(Location location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var key = GetCellKey(location.PixelX, location.PixelY);
            if (!_cells.TryGetValue(key, out var bucket))
            {
                bucket = new List<Location>();
                _cells[key] = bucket;
            }

            bucket.Add(location);
        }

        /// <summary>
        /// Inserts all locations into the grid.
        /// </summary>
        public void InsertAll(IEnumerable<Location> locations)
        {
            if (locations == null)
                throw new ArgumentNullException(nameof(locations));

            foreach (var location in locations)
                Insert(location);
        }

        /// <summary>
        /// Returns the cell indices for a pixel coordinate.
        /// </summary>
        public (int CellX, int CellY) GetCellKey(double pixelX, double pixelY)
        {
            // Floor division; negative coords still land in a stable cell.
            var cellX = (int)Math.Floor(pixelX / _cellSize);
            var cellY = (int)Math.Floor(pixelY / _cellSize);
            return (cellX, cellY);
        }

        /// <summary>
        /// Yields locations in the 3×3 Moore neighborhood centered on the cell
        /// that contains <paramref name="location"/> (including that cell).
        /// </summary>
        public IEnumerable<Location> GetCandidatesInNeighborhood(Location location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var (cx, cy) = GetCellKey(location.PixelX, location.PixelY);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!_cells.TryGetValue((cx + dx, cy + dy), out var bucket))
                        continue;

                    foreach (var candidate in bucket)
                        yield return candidate;
                }
            }
        }
    }
}
