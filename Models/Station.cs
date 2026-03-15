using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace ACO_Optimizer.Models
{
    public class Station : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Name { get; set; }

        private double _latitude;
        public double Latitude
        {
            get => _latitude;
            set { _latitude = value; OnPropertyChanged(nameof(Latitude)); }
        }

        private double _longitude;
        public double Longitude
        {
            get => _longitude;
            set { _longitude = value; OnPropertyChanged(nameof(Longitude)); }
        }

        private bool _isAvailable;
        public bool IsAvailable
        {
            get => _isAvailable;
            set { _isAvailable = value; OnPropertyChanged(nameof(IsAvailable)); }
        }

        private bool _isPricedCharger;
        public bool IsPricedCharger
        {
            get => _isPricedCharger;
            set { _isPricedCharger = value; OnPropertyChanged(nameof(IsPricedCharger)); }
        }

        // Existing: queue time in minutes (may be set by services)
        private double _queueTimeMin;
        public double QueueTimeMin
        {
            get => _queueTimeMin;
            set { _queueTimeMin = value; OnPropertyChanged(nameof(QueueTimeMin)); }
        }

        // New: amount of kWh in battery when arriving to this station (before charging)
        private double _energyOnArrivalKwh;
        public double EnergyOnArrivalKwh
        {
            get => _energyOnArrivalKwh;
            set { _energyOnArrivalKwh = value; OnPropertyChanged(nameof(EnergyOnArrivalKwh)); }
        }

        // New: amount of kWh in battery when leaving this station
        private double _energyOnDepartureKwh;
        public double EnergyOnDepartureKwh
        {
            get => _energyOnDepartureKwh;
            set { _energyOnDepartureKwh = value; OnPropertyChanged(nameof(EnergyOnDepartureKwh)); }
        }

        // New: time spent charging at this station (minutes)
        private double _chargingTimeMin;
        public double ChargingTimeMin
        {
            get => _chargingTimeMin;
            set { _chargingTimeMin = value; OnPropertyChanged(nameof(ChargingTimeMin)); }
        }

        // ChargerTypes now maps charger type name -> power (kW)
        private Dictionary<string, double> _chargerTypes;
        public Dictionary<string, double> ChargerTypes
        {
            get => _chargerTypes;
            set { _chargerTypes = value; OnPropertyChanged(nameof(ChargerTypes)); }
        }

        // New: whether the ant actually charged at this station during simulation
        private bool _wasChargedHere;
        public bool WasChargedHere
        {
            get => _wasChargedHere;
            set { _wasChargedHere = value; OnPropertyChanged(nameof(WasChargedHere)); }
        }

        // Road coordinates (lat, lon) to the next station in the route
        // Stores actual road coordinates for visualization
        private List<(double latitude, double longitude)> _roadCoordinatesTo;
        public List<(double latitude, double longitude)> RoadCoordinatesTo
        {
            get => _roadCoordinatesTo ?? new List<(double, double)>();
            set { _roadCoordinatesTo = value; OnPropertyChanged(nameof(RoadCoordinatesTo)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Create a shallow copy of this Station suitable for per-ant simulation.
        /// Copies static properties (id, name, location, charger definitions and queue time)
        /// but leaves diagnostic fields (EnergyOnArrivalKwh, EnergyOnDepartureKwh, ChargingTimeMin, WasChargedHere)
        /// at defaults so each Ant can record its own values without mutating shared graph instances.
        /// </summary>
        public Station CreateSimulationCopy()
        {
            return new Station
            {
                Id = this.Id,
                Name = this.Name,
                Latitude = this.Latitude,
                Longitude = this.Longitude,
                IsAvailable = this.IsAvailable,
                IsPricedCharger = this.IsPricedCharger,
                QueueTimeMin = this.QueueTimeMin,
                ChargerTypes = this.ChargerTypes != null ? new Dictionary<string, double>(this.ChargerTypes) : null
                // Note: diagnostic fields are intentionally left at defaults (0 / false)
            };
        }
    }
}
