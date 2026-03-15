namespace ACO_Optimizer.Services
{
    /// <summary>
    /// Distance provider interface for ACO algorithm
    /// </summary>
    public interface IDistanceProvider
    {
        /// <summary>
        /// Get distance between stations by indices
        /// </summary>
        /// <returns>Distance in km, or null if no connection</returns>
        double? GetDistanceKmByIndex(int fromIndex, int toIndex);

        /// <summary>
        /// Get distance between stations by ID
        /// </summary>
        /// <returns>Distance in km, or null if no connection</returns>
        double? GetDistanceKmById(string fromId, string toId);

        /// <summary>
        /// Maximum number of stations in the matrix
        /// </summary>
        int StationCount { get; }
    }
}
