using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACO_Optimizer.Models;

namespace ACO_Optimizer.Services
{
    /// <summary>
    /// Service for loading and managing OSRM data
    /// Integrates: distance-matrix.json, routes.jsonl.gz, stations.json
    /// </summary>
    public static class RoadNetworkService
    {
        private static IDistanceProvider _distanceProvider;
        private static IRouteRepository _routeRepository;
        private static List<StationData> _stations;

        /// <summary>
        /// Initialize service with OSRM files
        /// </summary>
        public static void Initialize(
            string stationsJsonPath,
            string distanceMatrixJsonPath,
            string routesGzipPath)
        {
            System.Diagnostics.Debug.WriteLine("Initializing OSRM data service...");

            try
            {
                // Load stations
                _stations = LoadStations(stationsJsonPath);
                System.Diagnostics.Debug.WriteLine($"Loaded {_stations.Count} stations");

                // Initialize distance provider
                _distanceProvider = new DistanceMatrixProvider(distanceMatrixJsonPath, _stations);
                System.Diagnostics.Debug.WriteLine("Distance matrix provider initialized");

                // Initialize route repository
                _routeRepository = new RouteRepositoryGzip(routesGzipPath, _stations.Count);
                System.Diagnostics.Debug.WriteLine("Route repository initialized");

                System.Diagnostics.Debug.WriteLine("OSRM data service ready");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing OSRM service: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get distance provider
        /// </summary>
        public static IDistanceProvider GetDistanceProvider()
        {
            if (_distanceProvider == null)
                throw new InvalidOperationException("Distance provider not initialized. Call Initialize() first.");
            return _distanceProvider;
        }

        /// <summary>
        /// Get route repository
        /// </summary>
        public static IRouteRepository GetRouteRepository()
        {
            if (_routeRepository == null)
                throw new InvalidOperationException("Route repository not initialized. Call Initialize() first.");
            return _routeRepository;
        }

        /// <summary>
        /// Get list of stations
        /// </summary>
        public static List<StationData> GetStations()
        {
            if (_stations == null)
                throw new InvalidOperationException("Stations not loaded. Call Initialize() first.");
            return _stations;
        }

        /// <summary>
        /// Load stations from JSON
        /// </summary>
        private static List<StationData> LoadStations(string stationsJsonPath)
        {
            if (!File.Exists(stationsJsonPath))
                throw new FileNotFoundException($"Stations file not found: {stationsJsonPath}");

            var json = File.ReadAllText(stationsJsonPath);
            using (var doc = System.Text.Json.JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                var stations = new List<StationData>();
                int index = 0;

                foreach (var item in root.EnumerateArray())
                {
                    var station = new StationData
                    {
                        Index = index,
                        Id = item.GetProperty("Id").GetString(),
                        Name = item.GetProperty("Name").GetString(),
                        Latitude = item.GetProperty("Latitude").GetDouble(),
                        Longitude = item.GetProperty("Longitude").GetDouble()
                    };
                    stations.Add(station);
                    index++;
                }

                return stations;
            }
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public static void Cleanup()
        {
            _distanceProvider = null;
            _routeRepository = null;
            _stations = null;
        }
    }
}
