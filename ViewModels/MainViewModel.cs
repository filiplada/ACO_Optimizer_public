using ACO_Optimizer.Helpers;
using ACO_Optimizer.Models;
using ACO_Optimizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace ACO_Optimizer.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Station> Stations { get; set; } = new ObservableCollection<Station>();
        public Route BestRoute { get; set; }
        public String TimeInTicks { get; set; }
        public bool IsPathUnachievable { get; set; } = false;
        public ACOParameters Parameters { get; set; } = new ACOParameters();
        public ObservableCollection<string> ChargerTypes { get; set; } = new ObservableCollection<string>
        {
            "Type 2 (Socket Only)",
            "Type 2 (Tethered Connector)",
            "CHAdeMO",
            "CCS (Type 2)"
        };

        public ICommand RunACOCommand { get; set; }

        private readonly string RELATIVE_JSON_PATH = "Data\\stations.json";
        private readonly ExportService _exportService;
        private long _lastCalculationTimeMs;

        public MainViewModel()
        {
            InitializeStations();
            _exportService = new ExportService();
            RunACOCommand = new RelayCommand(RunACO);
        }

        private void RunACO()
        {
            try
            {
                // Load actual data from OSRM
                var osrmStations = RoadNetworkService.GetStations();
                var distanceProvider = RoadNetworkService.GetDistanceProvider();
                var routeRepository = RoadNetworkService.GetRouteRepository();

                if (osrmStations == null || osrmStations.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: OSRM not initialized. No stations loaded.");
                    IsPathUnachievable = true;
                    OnPropertyChanged(nameof(IsPathUnachievable));
                    return;
                }

                // Build distance matrix from OSRM (ALWAYS from OSRM, never fallback!)
                var distances = new Dictionary<(string, string), double>();
                foreach (var from in Stations)
                {
                    foreach (var to in Stations)
                    {
                        if (from.Id != to.Id)
                        {
                            // Get from OSRM - if null, throw exception (no fallback!)
                            var fromData = osrmStations.FirstOrDefault(s => s.Id == from.Id);
                            var toData = osrmStations.FirstOrDefault(s => s.Id == to.Id);

                            if (fromData == null || toData == null)
                            {
                                throw new InvalidOperationException(
                                    $"Station not found in OSRM data: {from.Id} or {to.Id}");
                            }

                            var dist = distanceProvider.GetDistanceKmByIndex(fromData.Index, toData.Index);
                            
                            if (dist == null)
                            {
                                throw new InvalidOperationException(
                                    $"No distance in OSRM matrix: {from.Id} -> {to.Id}. Route doesn't exist.");
                            }

                            distances[(from.Id, to.Id)] = dist.Value;
                        }
                    }
                }

                var graph = new Graph
                {
                    Stations = Stations.ToList(),
                    Distances = distances
                };

                var aco = new ACOService(graph, Parameters);

                // Measure execution time
                Stopwatch stopWatch = new Stopwatch();

                stopWatch.Start();

                if (Parameters.StartingStation == Parameters.DestinationStation)
                {
                    // Do nothing, same station
                    BestRoute = null;
                }
                else if (Parameters.StartingStation != null)
                {
                    BestRoute = aco.Run(Parameters.StartingStation.Id);
                }
                else
                {
                    BestRoute = aco.Run(Stations.First().Id);
                }

                stopWatch.Stop();

                _lastCalculationTimeMs = stopWatch.ElapsedMilliseconds;
                TimeInTicks = _lastCalculationTimeMs.ToString();

                IsPathUnachievable = BestRoute == null;

                // Save results to CSV always (even if no route found)
                // JSON export only for successful routes
                if (Parameters.StartingStation != Parameters.DestinationStation)
                {
                    try
                    {
                        if (BestRoute != null)
                        {
                            System.Diagnostics.Debug.WriteLine("📝 Starting export of results...");
                            System.Diagnostics.Debug.WriteLine($"   Route: {Parameters.StartingStation.Name} → {Parameters.DestinationStation.Name}");
                            System.Diagnostics.Debug.WriteLine($"   Stations: {BestRoute.Stations.Count}, Distance: {BestRoute.TotalDistance:F2} km");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("📝 Starting export of results (no route found)...");
                            System.Diagnostics.Debug.WriteLine($"   Route attempt: {Parameters.StartingStation.Name} → {Parameters.DestinationStation.Name}");
                        }
                        
                        _exportService.ExportBoth(BestRoute, Parameters, _lastCalculationTimeMs);
                        
                        System.Diagnostics.Debug.WriteLine("✓ Export completed successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Error during export: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"   Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠ Starting station equals destination - skipping export");
                }

                // Always update UI list, calculation time, and route metrics
                // Only skip visual map updates if EnableVisualMapUpdate is false
                OnPropertyChanged(nameof(TimeInTicks));
                OnPropertyChanged(nameof(BestRoute));
                OnPropertyChanged(nameof(IsPathUnachievable));

                if (!Parameters.EnableVisualMapUpdate)
                {
                    System.Diagnostics.Debug.WriteLine("⏩ Map update skipped (EnableVisualMapUpdate = false)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in RunACO: {ex.Message}");
                IsPathUnachievable = true;
                OnPropertyChanged(nameof(IsPathUnachievable));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        
        private void InitializeStations()
        {
            var loadedStations = DataService.LoadStationsFromJson(RELATIVE_JSON_PATH);
            Stations.Clear();
            foreach (var station in loadedStations)
            {
                Stations.Add(station);
            }

            // Set default starting station to Poznań (S5) if available
            if (Stations.Any() && Parameters.StartingStation == null)
            {
                var defaultStarting = Stations.FirstOrDefault(s => s.Id == Parameters.DefaultStartingStationId);
                Parameters.StartingStation = defaultStarting ?? Stations.First();
            }

            // Set default destination station to Kraków (S2) if available
            if (Stations.Any() && Parameters.DestinationStation == null)
            {
                var defaultDestination = Stations.FirstOrDefault(s => s.Id == Parameters.DefaultDestinationStationId);
                Parameters.DestinationStation = defaultDestination ?? Stations.First();
            }
        }
    }
}

