using System.Collections.Generic;

namespace ACO_Optimizer.Models
{
    public class Graph
    {
        public List<Station> Stations { get; set; } = new List<Station>();
        public Dictionary<(string, string), double> Distances { get; set; } = new Dictionary<(string, string), double>();

        public double GetDistance(string fromId, string toId)
        {
            return Distances.TryGetValue((fromId, toId), out var dist) ? dist : double.MaxValue;
        }
    }
}
