using System.Collections.Generic;
using ACO_Optimizer.Models;

namespace ACO_Optimizer.Services
{
    /// <summary>
    /// Route repository interface (geometry + metadata)
    /// </summary>
    public interface IRouteRepository
    {
        /// <summary>
        /// Get route between stations by indices
        /// </summary>
        /// <param name="fromIndex">Starting station index</param>
        /// <param name="toIndex">Destination station index</param>
        /// <param name="withGeometry">Whether to load full geometry (may be slow for large files)</param>
        /// <returns>Route data, or null if not found</returns>
        RouteEdge GetEdgeByIndex(int fromIndex, int toIndex, bool withGeometry = false);

        /// <summary>
        /// Get all available edges (for diagnostics/cache)
        /// </summary>
        IEnumerable<RouteEdgeHeader> GetAllEdgeHeaders();
    }
}
