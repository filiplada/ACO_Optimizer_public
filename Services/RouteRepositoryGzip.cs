using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ACO_Optimizer.Models;

namespace ACO_Optimizer.Services
{
    /// <summary>
    /// Route repository that streams from routes.jsonl.gz
    /// Builds lightweight index (without geometry) for fast access
    /// </summary>
    public class RouteRepositoryGzip : IRouteRepository
    {
        private readonly string _routesGzipPath;
        private readonly Dictionary<string, RouteEdgeHeader> _edgeIndex;
        private readonly int _stationCount;

        /// <summary>
        /// Initialize repository and build index
        /// </summary>
        public RouteRepositoryGzip(string routesGzipPath, int stationCount)
        {
            if (!File.Exists(routesGzipPath))
                throw new FileNotFoundException($"Routes file not found: {routesGzipPath}");

            _routesGzipPath = routesGzipPath;
            _stationCount = stationCount;
            _edgeIndex = new Dictionary<string, RouteEdgeHeader>();

            // Zbuduj indeks (bez geometrii - tylko metadata)
            BuildIndex();
        }

        /// <summary>
        /// Build route index - iterate through entire file and remember metadata of each line
        /// </summary>
        private void BuildIndex()
        {
            System.Diagnostics.Debug.WriteLine("Building route index from routes.jsonl.gz...");

            int lineCount = 0;
            FileStream fs = null;
            GZipStream gz = null;
            StreamReader sr = null;

            try
            {
                fs = File.OpenRead(_routesGzipPath);
                gz = new GZipStream(fs, CompressionMode.Decompress);
                sr = new StreamReader(gz, Encoding.UTF8);

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lineCount++;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var edge = ParseRouteLine(line, includeGeometry: false);
                        string key = $"{edge.FromIndex}:{edge.ToIndex}";
                        _edgeIndex[key] = edge;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Warning: Failed to parse route line {lineCount}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"Route index built: {_edgeIndex.Count} edges from {lineCount} lines");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to build route index from {_routesGzipPath}", ex);
            }
            finally
            {
                if (sr != null) sr.Dispose();
                if (gz != null) gz.Dispose();
                if (fs != null) fs.Dispose();
            }
        }

        public RouteEdge GetEdgeByIndex(int fromIndex, int toIndex, bool withGeometry = false)
        {
            string key = $"{fromIndex}:{toIndex}";
            if (!_edgeIndex.TryGetValue(key, out var header))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Route not found: [{fromIndex}] -> [{toIndex}]");
                return null;
            }

            // If geometry is not needed, return header copy without geometry
            if (!withGeometry)
            {
                return new RouteEdge
                {
                    FromIndex = header.FromIndex,
                    ToIndex = header.ToIndex,
                    FromId = header.FromId,
                    ToId = header.ToId,
                    DistanceKm = header.DistanceKm,
                    DurationSeconds = header.DurationSeconds,
                    GeometryPoints = new List<(double, double)>()
                };
            }

            // If geometry is needed - read from file again (lazy load)
            return GetEdgeWithGeometry(fromIndex, toIndex, header);
        }

        /// <summary>
        /// Get route with geometry - read from file again
        /// Slow, but saves memory
        /// </summary>
        private RouteEdge GetEdgeWithGeometry(int fromIndex, int toIndex, RouteEdgeHeader header)
        {
            FileStream fs = null;
            GZipStream gz = null;
            StreamReader sr = null;

            try
            {
                fs = File.OpenRead(_routesGzipPath);
                gz = new GZipStream(fs, CompressionMode.Decompress);
                sr = new StreamReader(gz, Encoding.UTF8);

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var edge = ParseRouteLine(line, includeGeometry: true);
                    if (edge.FromIndex == fromIndex && edge.ToIndex == toIndex)
                    {
                        return edge;
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"Warning: Route [{fromIndex}]->[{toIndex}] not found in second pass");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error reading route geometry [{fromIndex}]->[{toIndex}]: {ex.Message}");
                return null;
            }
            finally
            {
                if (sr != null) sr.Dispose();
                if (gz != null) gz.Dispose();
                if (fs != null) fs.Dispose();
            }
        }

        /// <summary>
        /// Parse one NDJSON line from routes.jsonl.gz
        /// </summary>
        private RouteEdge ParseRouteLine(string jsonLine, bool includeGeometry)
        {
            using (var doc = JsonDocument.Parse(jsonLine))
            {
                var root = doc.RootElement;

                int fromIndex = root.GetProperty("fromIndex").GetInt32();
                int toIndex = root.GetProperty("toIndex").GetInt32();
                string fromId = root.GetProperty("fromId").GetString();
                string toId = root.GetProperty("toId").GetString();
                double distanceKm = root.GetProperty("distanceKm").GetDouble();
                double durationS = root.GetProperty("durationS").GetDouble();

                var edge = new RouteEdge
                {
                    FromIndex = fromIndex,
                    ToIndex = toIndex,
                    FromId = fromId,
                    ToId = toId,
                    DistanceKm = distanceKm,
                    DurationSeconds = durationS,
                    GeometryPoints = new List<(double, double)>()
                };

                // Optionally load geometry
                if (includeGeometry && root.TryGetProperty("geometry", out var geom))
                {
                    edge.GeometryPoints = ParseGeometry(geom);
                }

                return edge;
            }
        }

        /// <summary>
        /// Parse GeoJSON LineString and return list of (lon, lat)
        /// </summary>
        private List<(double Longitude, double Latitude)> ParseGeometry(JsonElement geometry)
        {
            var points = new List<(double, double)>();

            try
            {
                if (geometry.GetProperty("type").GetString() != "LineString")
                {
                    System.Diagnostics.Debug.WriteLine(
                        "Warning: Expected LineString geometry, got: " +
                        geometry.GetProperty("type").GetString());
                    return points;
                }

                var coordinates = geometry.GetProperty("coordinates");
                foreach (var coord in coordinates.EnumerateArray())
                {
                    if (coord.GetArrayLength() >= 2)
                    {
                        double lon = coord[0].GetDouble();
                        double lat = coord[1].GetDouble();
                        points.Add((lon, lat));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing geometry: {ex.Message}");
            }

            return points;
        }

        public IEnumerable<RouteEdgeHeader> GetAllEdgeHeaders()
        {
            return _edgeIndex.Values;
        }
    }
}
