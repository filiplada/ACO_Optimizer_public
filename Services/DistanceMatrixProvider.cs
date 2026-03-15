using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ACO_Optimizer.Models;

namespace ACO_Optimizer.Services
{
    /// <summary>
    /// Distance provider from distance-matrix.json file
    /// Loads entire matrix into memory (fast access, O(1))
    /// </summary>
    public class DistanceMatrixProvider : IDistanceProvider
    {
        private readonly double?[] _distances;
        private readonly int _size;
        private readonly Dictionary<string, int> _stationIdToIndex;

        public int StationCount => _size;

        /// <summary>
        /// Load matrix from JSON file
        /// </summary>
        public DistanceMatrixProvider(string distanceMatrixJsonPath, List<StationData> stations)
        {
            if (!File.Exists(distanceMatrixJsonPath))
                throw new FileNotFoundException($"Distance matrix file not found: {distanceMatrixJsonPath}");

            // Load and parse JSON
            var json = File.ReadAllText(distanceMatrixJsonPath);
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                _size = root.GetProperty("size").GetInt32();

                // Verify consistency with station list
                if (stations.Count != _size)
                    throw new InvalidOperationException(
                        $"Station count ({stations.Count}) doesn't match matrix size ({_size})");

                // Initialize ID -> index
                _stationIdToIndex = new Dictionary<string, int>(_size);
                foreach (var station in stations)
                {
                    _stationIdToIndex[station.Id] = station.Index;
                }

                // Load distances into flat array: index = i * size + j
                _distances = new double?[_size * _size];

                var distancesArray = root.GetProperty("distances");
                int rowIndex = 0;

                foreach (var row in distancesArray.EnumerateArray())
                {
                    int colIndex = 0;
                    foreach (var value in row.EnumerateArray())
                    {
                        // Distances may be null (no connection)
                        double? distance = value.ValueKind == JsonValueKind.Null
                            ? (double?)null
                            : value.GetDouble();

                        _distances[rowIndex * _size + colIndex] = distance;
                        colIndex++;
                    }
                    rowIndex++;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"Distance matrix loaded: {_size}x{_size} ({_distances.Length} entries)");
            }
        }

        public double? GetDistanceKmByIndex(int fromIndex, int toIndex)
        {
            // Bounds validation
            if (fromIndex < 0 || fromIndex >= _size || toIndex < 0 || toIndex >= _size)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Invalid station indices: [{fromIndex}] -> [{toIndex}], max: {_size}");
                return null;
            }

            var distance = _distances[fromIndex * _size + toIndex];

            if (!distance.HasValue)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"No distance found between stations [{fromIndex}] -> [{toIndex}]");
            }

            return distance;
        }

        public double? GetDistanceKmById(string fromId, string toId)
        {
            if (!_stationIdToIndex.TryGetValue(fromId, out int fromIdx))
            {
                System.Diagnostics.Debug.WriteLine($"Unknown station ID: {fromId}");
                return null;
            }

            if (!_stationIdToIndex.TryGetValue(toId, out int toIdx))
            {
                System.Diagnostics.Debug.WriteLine($"Unknown station ID: {toId}");
                return null;
            }

            return GetDistanceKmByIndex(fromIdx, toIdx);
        }
    }
}
