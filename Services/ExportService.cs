using ACO_Optimizer.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ACO_Optimizer.Services
{
    public class ExportService
    {
        private readonly string _exportDirectory = "Results";
        private const string CSV_SEPARATOR = ";";

        public ExportService()
        {
            // Utwórz katalog Results jeœli nie istnieje
            try
            {
                if (!Directory.Exists(_exportDirectory))
                {
                    Directory.CreateDirectory(_exportDirectory);
                    System.Diagnostics.Debug.WriteLine($"? Created Results directory: {Path.GetFullPath(_exportDirectory)}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"? Results directory exists: {Path.GetFullPath(_exportDirectory)}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error creating Results directory: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Eksportuje wynik symulacji do pliku JSON z pe³nymi danymi
        /// </summary>
        public void ExportToJson(Route route, ACOParameters parameters, long calculationTimeMs, DateTime timestamp)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route), "Nie mo¿na zapisaæ pustej trasy");

            try
            {
                var simulationResult = new SimulationResult
                {
                    Timestamp = timestamp,
                    CalculationTimeMs = calculationTimeMs,
                    Parameters = new ParametersSnapshot(parameters),
                    RouteData = new RouteSnapshot(route)
                };

                string fileName = $"result_{timestamp:yyyy-MM-dd_HH-mm-ss-fff}.json";
                string filePath = Path.Combine(_exportDirectory, fileName);

                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                string json = JsonConvert.SerializeObject(simulationResult, jsonSettings);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                
                System.Diagnostics.Debug.WriteLine($"  ? JSON exported: {Path.GetFullPath(filePath)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"  ? JSON export failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Eksportuje wynik symulacji do CSV (agregowany format dla analizy)
        /// </summary>
        public void ExportToCsv(Route route, ACOParameters parameters, long calculationTimeMs, DateTime timestamp, bool appendToExisting = true)
        {
            try
            {
                string csvFileName = "simulation_results.csv";
                string csvFilePath = Path.Combine(_exportDirectory, csvFileName);
                bool fileExists = File.Exists(csvFilePath);

                // Przygotuj nag³ówek CSV - nazwy takie same jak na widoku, bez znaku dwukropka
                // Separacja œrednikami
                string header = "Timestamp" + CSV_SEPARATOR + "Punkt Startowy" + CSV_SEPARATOR + "Punkt Docelowy" + CSV_SEPARATOR + 
                               "Ca³kowity Dystans" + CSV_SEPARATOR + "Czas Podró¿y" + CSV_SEPARATOR + "Czas Kalkulacji" + CSV_SEPARATOR +
                               "Iteracja Najlepszej trasy" + CSV_SEPARATOR +
                               "Liczba Stacji" + CSV_SEPARATOR + "Liczba Mrówek" + CSV_SEPARATOR + "Iteracje" + CSV_SEPARATOR + 
                               "Alpha" + CSV_SEPARATOR + "Beta" + CSV_SEPARATOR + "WskaŸnik Parowania" + CSV_SEPARATOR + 
                               "Pocz¹tkowy Feromon" + CSV_SEPARATOR + "Pojemnoœæ Baterii" + CSV_SEPARATOR + "Pocz¹tkowy Stan Baterii" + 
                               CSV_SEPARATOR + "Konsumpcja" + CSV_SEPARATOR + "Tylko £adowarki Z Cennikiem" + CSV_SEPARATOR + 
                               "Typ £adowarki" + CSV_SEPARATOR + "Symulacja równoleg³a";

                // Przygotuj dane wiersza
                string dataRow;
                if (route == null)
                {
                    // Brak trasy - zapisz BRAK_TRASY dla dystansu, czasu podró¿y i liczby stacji
                    dataRow = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff") + CSV_SEPARATOR +
                               "\"" + (parameters.StartingStation?.Name ?? "N/A") + "\"" + CSV_SEPARATOR +
                               "\"" + (parameters.DestinationStation?.Name ?? "N/A") + "\"" + CSV_SEPARATOR +
                               "BRAK_TRASY" + CSV_SEPARATOR +
                               "BRAK_TRASY" + CSV_SEPARATOR +
                               calculationTimeMs.ToString() + CSV_SEPARATOR +
                               "BRAK_TRASY" + CSV_SEPARATOR +
                               "BRAK_TRASY" + CSV_SEPARATOR +
                               parameters.NumberOfAnts.ToString() + CSV_SEPARATOR +
                               parameters.Iterations.ToString() + CSV_SEPARATOR +
                               parameters.Alpha.ToString() + CSV_SEPARATOR +
                               parameters.Beta.ToString() + CSV_SEPARATOR +
                               parameters.EvaporationRate.ToString() + CSV_SEPARATOR +
                               parameters.InitialPheromone.ToString() + CSV_SEPARATOR +
                               parameters.BatteryCapacity.ToString() + CSV_SEPARATOR +
                               parameters.InitialBattery.ToString() + CSV_SEPARATOR +
                               parameters.ConsumptionPerKm.ToString() + CSV_SEPARATOR +
                               parameters.OnlyPricedChargers.ToString() + CSV_SEPARATOR +
                               "\"" + parameters.ChargerType + "\"" + CSV_SEPARATOR +
                               parameters.EnableParallelSimulation.ToString();
                }
                else
                {
                    // Mamy trasê - zaokr¹glij wartoœci jak na widoku
                    // Liczby dziesiêtne z przecinkiem (polska notacja)
                    dataRow = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff") + CSV_SEPARATOR +
                               "\"" + (parameters.StartingStation?.Name ?? "N/A") + "\"" + CSV_SEPARATOR +
                               "\"" + (parameters.DestinationStation?.Name ?? "N/A") + "\"" + CSV_SEPARATOR +
                               ((int)Math.Round(route.TotalDistance)).ToString() + CSV_SEPARATOR +
                               ((int)Math.Round(route.TotalTime)).ToString() + CSV_SEPARATOR +
                               calculationTimeMs.ToString() + CSV_SEPARATOR +
                               route.IterationOfBestSolution.ToString() + CSV_SEPARATOR +
                               route.Stations.Count.ToString() + CSV_SEPARATOR +
                               parameters.NumberOfAnts.ToString() + CSV_SEPARATOR +
                               parameters.Iterations.ToString() + CSV_SEPARATOR +
                               parameters.Alpha.ToString() + CSV_SEPARATOR +
                               parameters.Beta.ToString() + CSV_SEPARATOR +
                               parameters.EvaporationRate.ToString() + CSV_SEPARATOR +
                               parameters.InitialPheromone.ToString() + CSV_SEPARATOR +
                               parameters.BatteryCapacity.ToString() + CSV_SEPARATOR +
                               parameters.InitialBattery.ToString() + CSV_SEPARATOR +
                               parameters.ConsumptionPerKm.ToString() + CSV_SEPARATOR +
                               parameters.OnlyPricedChargers.ToString() + CSV_SEPARATOR +
                               "\"" + parameters.ChargerType + "\"" + CSV_SEPARATOR +
                               parameters.EnableParallelSimulation.ToString();
                }

                using (var writer = new StreamWriter(csvFilePath, append: appendToExisting && fileExists, Encoding.UTF8))
                {
                    // Napisz nag³ówek tylko jeœli plik nie istnieje
                    if (!fileExists)
                    {
                        writer.WriteLine(header);
                    }

                    writer.WriteLine(dataRow);
                }
                
                System.Diagnostics.Debug.WriteLine($"  ? CSV exported: {Path.GetFullPath(csvFilePath)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"  ? CSV export failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Eksportuje wyniki do obu formatów (JSON + CSV)
        /// </summary>
        public void ExportBoth(Route route, ACOParameters parameters, long calculationTimeMs)
        {
            DateTime timestamp = DateTime.Now;
            
            // Eksportuj JSON tylko jeœli mamy trasê
            if (route != null)
            {
                ExportToJson(route, parameters, calculationTimeMs, timestamp);
            }
            
            // Eksportuj CSV zawsze (nawet jeœli brak trasy)
            ExportToCsv(route, parameters, calculationTimeMs, timestamp);
        }
    }

    /// <summary>
    /// Snapshot parametrów ACO dla archiwizacji
    /// </summary>
    public class ParametersSnapshot
    {
        public int NumberOfAnts { get; set; }
        public int Iterations { get; set; }
        public double Alpha { get; set; }
        public double Beta { get; set; }
        public double EvaporationRate { get; set; }
        public double InitialPheromone { get; set; }
        public string StartingStationName { get; set; }
        public string DestinationStationName { get; set; }
        public double BatteryCapacity { get; set; }
        public double InitialBattery { get; set; }
        public double ConsumptionPerKm { get; set; }
        public bool OnlyPricedChargers { get; set; }
        public string ChargerType { get; set; }
        public bool EnableParallelSimulation { get; set; }

        public ParametersSnapshot(ACOParameters parameters)
        {
            NumberOfAnts = parameters.NumberOfAnts;
            Iterations = parameters.Iterations;
            Alpha = parameters.Alpha;
            Beta = parameters.Beta;
            EvaporationRate = parameters.EvaporationRate;
            InitialPheromone = parameters.InitialPheromone;
            StartingStationName = parameters.StartingStation?.Name;
            DestinationStationName = parameters.DestinationStation?.Name;
            BatteryCapacity = parameters.BatteryCapacity;
            InitialBattery = parameters.InitialBattery;
            ConsumptionPerKm = parameters.ConsumptionPerKm;
            OnlyPricedChargers = parameters.OnlyPricedChargers;
            ChargerType = parameters.ChargerType;
            EnableParallelSimulation = parameters.EnableParallelSimulation;
        }
    }

    /// <summary>
    /// Snapshot trasy dla archiwizacji
    /// </summary>
    public class RouteSnapshot
    {
        public int TotalDistance { get; set; }
        public int TotalTime { get; set; }
        public int IterationOfBestSolution { get; set; }
        public int StationCount { get; set; }
        public List<StationSnapshot> Stations { get; set; } = new List<StationSnapshot>();

        public RouteSnapshot(Route route)
        {
            // Zaokr¹glij dystans i czas tak jak na widoku
            TotalDistance = (int)Math.Round(route.TotalDistance);
            TotalTime = (int)Math.Round(route.TotalTime);
            IterationOfBestSolution = route.IterationOfBestSolution;
            StationCount = route.Stations.Count;
            Stations = route.Stations.Select((s, index) => new StationSnapshot(s, index)).ToList();
        }
    }

    /// <summary>
    /// Snapshot stacji dla archiwizacji
    /// </summary>
    public class StationSnapshot
    {
        public int SequenceNumber { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int EnergyOnArrivalKwh { get; set; }
        public int EnergyOnDepartureKwh { get; set; }
        public int ChargingTimeMin { get; set; }
        public int QueueTimeMin { get; set; }
        public bool WasChargedHere { get; set; }
        public int TotalEnergyConsumedAtStation => EnergyOnArrivalKwh - EnergyOnDepartureKwh;

        public StationSnapshot(Station station, int sequenceNumber)
        {
            SequenceNumber = sequenceNumber + 1;
            Name = station.Name;
            Latitude = station.Latitude;
            Longitude = station.Longitude;
            // Zaokr¹glij wartoœci baterii i czasu tak jak na widoku
            EnergyOnArrivalKwh = (int)Math.Round(station.EnergyOnArrivalKwh);
            EnergyOnDepartureKwh = (int)Math.Round(station.EnergyOnDepartureKwh);
            ChargingTimeMin = (int)Math.Round(station.ChargingTimeMin);
            QueueTimeMin = (int)Math.Round(station.QueueTimeMin);
            WasChargedHere = station.WasChargedHere;
        }
    }

    /// <summary>
    /// Kompletny wynik symulacji z metadanymi
    /// </summary>
    public class SimulationResult
    {
        public DateTime Timestamp { get; set; }
        public long CalculationTimeMs { get; set; }
        public ParametersSnapshot Parameters { get; set; }
        public RouteSnapshot RouteData { get; set; }
    }
}
