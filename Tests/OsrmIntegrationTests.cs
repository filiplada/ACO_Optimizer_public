using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACO_Optimizer.Models;
using ACO_Optimizer.Services;

namespace ACO_Optimizer.Tests
{
    /// <summary>
    /// Testy jednostkowe dla integracji OSRM
    /// </summary>
    public class OsrmIntegrationTests
    {
        /// <summary>
        /// Test 1: Wczytanie macierzy dystansów
        /// </summary>
        public static void TestDistanceMatrixProvider()
        {
            Console.WriteLine("=== Test 1: Distance Matrix Provider ===");

            try
            {
                // Przyklad: zaladuj z plikow testowych
                var stations = new List<StationData>
                {
                    new StationData { Index = 0, Id = "KRK", Name = "Kraków", Latitude = 50.0646, Longitude = 19.9450 },
                    new StationData { Index = 1, Id = "WAW", Name = "Warszawa", Latitude = 52.2297, Longitude = 21.0122 }
                };

                // Przyk?³ad macierzy (podaj rzeczywista sciezke)
                string matrixPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "distance-matrix.json");

                if (!File.Exists(matrixPath))
                {
                    Console.WriteLine($"SKIP: Matrix file not found at {matrixPath}");
                    return;
                }

                var provider = new DistanceMatrixProvider(matrixPath, stations);

                // Test: pobierz dystans
                var distance = provider.GetDistanceKmByIndex(0, 1);
                Console.WriteLine($"Distance [0]->[1]: {distance} km");

                // Test: sprawdŸ granice
                var invalidDistance = provider.GetDistanceKmByIndex(99, 99);
                Console.WriteLine($"Invalid index result: {invalidDistance} (should be null)");

                Console.WriteLine("? Distance Matrix Provider test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test 2: Wczytanie tras z geometri¹
        /// </summary>
        public static void TestRouteRepository()
        {
            Console.WriteLine("\n=== Test 2: Route Repository ===");

            try
            {
                string routesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "routes.jsonl.gz");

                if (!File.Exists(routesPath))
                {
                    Console.WriteLine($"SKIP: Routes file not found at {routesPath}");
                    return;
                }

                var repo = new RouteRepositoryGzip(routesPath, 999); // 999 stacji

                // Test: pobierz trasê bez geometrii
                var edge = repo.GetEdgeByIndex(0, 1, withGeometry: false);
                if (edge != null)
                {
                    Console.WriteLine($"Edge [0]->[1]: {edge.DistanceKm} km, {edge.DurationSeconds} sec");
                }

                // Test: pobierz trasê z geometri¹ (wolniejsze!)
                var edgeWithGeo = repo.GetEdgeByIndex(0, 1, withGeometry: true);
                if (edgeWithGeo != null && edgeWithGeo.GeometryPoints.Count > 0)
                {
                    Console.WriteLine($"Geometry points: {edgeWithGeo.GeometryPoints.Count}");
                    Console.WriteLine($"First point: ({edgeWithGeo.GeometryPoints[0].Longitude}, {edgeWithGeo.GeometryPoints[0].Latitude})");
                }

                Console.WriteLine("? Route Repository test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test 3: Integracja z ACO
        /// </summary>
        public static void TestACOWithOSRM()
        {
            Console.WriteLine("\n=== Test 3: ACO with OSRM ===");

            try
            {
                // Zainicjalizuj serwis OSRM
                var stationsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "stations.json");
                var matrixPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "distance-matrix.json");
                var routesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "routes.jsonl.gz");

                if (!File.Exists(stationsPath) || !File.Exists(matrixPath) || !File.Exists(routesPath))
                {
                    Console.WriteLine("SKIP: Not all required files found");
                    return;
                }

                RoadNetworkService.Initialize(stationsPath, matrixPath, routesPath);

                var stations = RoadNetworkService.GetStations();
                var distanceProvider = RoadNetworkService.GetDistanceProvider();

                Console.WriteLine($"Loaded {stations.Count} stations");

                // Konwertuj na model Station do ACO
                var graphStations = stations.Select(s => new Station
                {
                    Id = s.Id,
                    Name = s.Name,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    IsAvailable = true
                }).ToList();

                // Zbuduj macierz dystansów dla ACO
                var distances = new Dictionary<(string, string), double>();
                foreach (var from in stations)
                {
                    foreach (var to in stations)
                    {
                        if (from.Id != to.Id)
                        {
                            var dist = distanceProvider.GetDistanceKmByIndex(from.Index, to.Index);
                            distances[(from.Id, to.Id)] = dist ?? double.MaxValue;
                        }
                    }
                }

                var graph = new Graph
                {
                    Stations = graphStations,
                    Distances = distances
                };

                // Uruchom mini ACO
                var parameters = new ACOParameters
                {
                    Iterations = 3,
                    NumberOfAnts = 5,
                    Alpha = 1.0,
                    Beta = 2.0,
                    StartingStation = graphStations.FirstOrDefault(),
                    DestinationStation = graphStations.LastOrDefault() ?? graphStations.First()
                };

                var aco = new ACOService(graph, parameters);
                var route = aco.Run(parameters.StartingStation?.Id ?? graphStations[0].Id);

                if (route != null)
                {
                    Console.WriteLine($"? Route found: {route.Stations.Count} stations, {route.TotalDistance:F2} km");
                }
                else
                {
                    Console.WriteLine("? No route found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
