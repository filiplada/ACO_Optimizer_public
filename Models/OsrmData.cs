using System;
using System.Collections.Generic;

namespace ACO_Optimizer.Models
{
    /// <summary>
    /// Station data (extended with index for quick access to matrix)
    /// </summary>
    public class StationData
    {
        public int Index { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    /// <summary>
    /// Route edge information (without geometry for speed)
    /// </summary>
    public class RouteEdgeHeader
    {
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }
        public string FromId { get; set; }
        public string ToId { get; set; }
        public double DistanceKm { get; set; }
        public double DurationSeconds { get; set; }
    }

    /// <summary>
    /// Full route edge with geometry (linestring)
    /// </summary>
    public class RouteEdge : RouteEdgeHeader
    {
        /// <summary>
        /// Route geometry: list of (lon, lat) in OSRM order
        /// </summary>
        public List<(double Longitude, double Latitude)> GeometryPoints { get; set; }
            = new List<(double, double)>();
    }

    /// <summary>
    /// Distance matrix metadata
    /// </summary>
    public class DistanceMatrixMetadata
    {
        public string Unit { get; set; } = "kilometers";
        public int Size { get; set; }
    }
}
