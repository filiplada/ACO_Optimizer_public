
namespace ACO_Optimizer.Models
{
    public class ACOParameters
    {
        /// <summary>
        /// Enable visual map updating and route rendering on the UI.
        /// If false, map updates are skipped for faster batch simulations/testing.
        /// Result display and saving still work normally.
        /// </summary>
        public bool EnableVisualMapUpdate { get; set; } = false;



        public int NumberOfAnts { get; set; } = 50;
        public int Iterations { get; set; } = 50;
        public double Alpha { get; set; } = 0.5; // pheromone influence
        public double Beta { get; set; } = 2.0;  // heuristic influence -  higher than pheromone influence to promote exploration
        public double EvaporationRate { get; set; } = 0.3;
        public double InitialPheromone { get; set; } = 1.0;
        
        /// <summary>
        /// Probability that an ant will charge even when it can reach the next station.
        /// Introduces exploration to avoid dead-ends on long routes.
        /// Recommended value: 0.10 - 0.20
        /// </summary>
        public double ChargeExplorationProbability { get; set; } = 0.15;

        /// <summary>
        /// Enable parallel simulation where ants are processed concurrently.
        /// If false, ants are processed sequentially (original behavior).
        /// </summary>
        public bool EnableParallelSimulation { get; set; } = true;

        public Station StartingStation { get; set; }
        public Station DestinationStation { get; set; }
        
        /// <summary>
        /// Default starting station ID for initialization (Poznań = "S5")
        /// </summary>
        public string DefaultStartingStationId { get; set; } = "S6";
        
        /// <summary>
        /// Default destination station ID for initialization (Kraków = "S2")
        /// </summary>
        public string DefaultDestinationStationId { get; set; } = "S39";



        public double BatteryCapacity { get; set; } = 75;// kWh, average EV battery capacity
        public double InitialBattery { get; set; } = 40; // kWh, half capacity at start
        public double ConsumptionPerKm { get; set; } = 0.25; // kWh/km, Tesla Model S

        public bool OnlyPricedChargers { get; set; } = false;
        public string ChargerType { get; set; } = "Type 2 (Socket Only)";
    }
}
