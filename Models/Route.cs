using System.Collections.Generic;

namespace ACO_Optimizer.Models
{
    public class Route
    {
        public List<Station> Stations { get; set; } = new List<Station>();
        public double TotalDistance { get; set; }
        public double TotalTime { get; set; }
        /// <summary>
        /// Numer iteracji, w której znaleziono tę trasę (1-indexed)
        /// </summary>
        public int IterationOfBestSolution { get; set; }
    }
}
