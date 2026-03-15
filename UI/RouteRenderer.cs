using System;
using System.Collections.Generic;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using ACO_Optimizer.Models;
using ACO_Optimizer.Services;

namespace ACO_Optimizer.UI
{
    /// <summary>
    /// Route renderer on Mapsui map with support for OSRM geometry
    /// Compatible with Mapsui 3.x+ (uses NetTopologySuite geometries)
    /// </summary>
    public class RouteRenderer
    {
        private readonly Map _map;
        private MemoryLayer _routeLayer;
        private GeometryFactory _geomFactory = new GeometryFactory();

        public RouteRenderer(Map map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// Render route based on station order
        /// </summary>
        public void RenderRouteByStationOrder(
            IReadOnlyList<StationData> stations,
            IReadOnlyList<int> stationOrderIndices,
            IRouteRepository routeRepository = null,
            Color strokeColor = null,
            float width = 3)
        {
            Clear();

            if (stationOrderIndices == null || stationOrderIndices.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No stations to render");
                return;
            }

            strokeColor = strokeColor ?? new Color { R = 255, G = 0, B = 0, A = 255 }; // Red
            var lineCoordinates = new List<Coordinate>();

            System.Diagnostics.Debug.WriteLine(
                $"Rendering route with {stationOrderIndices.Count} stations");

            // For each pair of consecutive stations
            for (int i = 0; i < stationOrderIndices.Count - 1; i++)
            {
                int fromIdx = stationOrderIndices[i];
                int toIdx = stationOrderIndices[i + 1];

                var fromStation = stations[fromIdx];
                var toStation = stations[toIdx];

                // Try to get geometry from route repository
                var routeSegment = routeRepository?.GetEdgeByIndex(fromIdx, toIdx, withGeometry: true);

                if (routeSegment?.GeometryPoints != null && routeSegment.GeometryPoints.Count > 0)
                {
                    // We have geometry from OSRM - convert to Mapsui (EPSG:3857)
                    AddGeometrySegment(lineCoordinates, routeSegment.GeometryPoints);
                }
                else
                {
                    // No geometry - add linear connection
                    AddDirectSegment(lineCoordinates, fromStation, toStation);
                }
            }

            // Add last station
            if (stationOrderIndices.Count > 0)
            {
                var lastStation = stations[stationOrderIndices[stationOrderIndices.Count - 1]];
                var lastPoint = SphericalMercator.FromLonLat(lastStation.Longitude, lastStation.Latitude);
                lineCoordinates.Add(new Coordinate(lastPoint.x, lastPoint.y));
            }

            // Create LineString and feature
            if (lineCoordinates.Count < 2)
            {
                System.Diagnostics.Debug.WriteLine("Route has less than 2 points, skipping render");
                return;
            }

            try
            {
                var lineString = _geomFactory.CreateLineString(lineCoordinates.ToArray());
                var feature = new GeometryFeature
                {
                    Geometry = lineString,
                    Styles = new List<IStyle>
                    {
                        new VectorStyle
                        {
                            Line = new Pen
                            {
                                Color = strokeColor,
                                Width = width,
                                PenStyle = PenStyle.Solid
                            }
                        }
                    }
                };

                // Create and add layer
                _routeLayer = new MemoryLayer
                {
                    Name = "Route",
                    Features = new List<IFeature> { feature }
                };

                _map.Layers.Add(_routeLayer);

                // Set map to route bounds
                FitToRoute(lineString);

                System.Diagnostics.Debug.WriteLine(
                    $"Route rendered: {lineCoordinates.Count} points");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rendering route: {ex.Message}");
            }
        }

        /// <summary>
        /// Add geometry segment from OSRM
        /// </summary>
        private void AddGeometrySegment(List<Coordinate> lineCoordinates, List<(double lon, double lat)> geometryPoints)
        {
            foreach (var (lon, lat) in geometryPoints)
            {
                try
                {
                    var mercPoint = SphericalMercator.FromLonLat(lon, lat);
                    lineCoordinates.Add(new Coordinate(mercPoint.x, mercPoint.y));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error converting coordinate {lon},{lat}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Add linear segment (fallback if no geometry available)
        /// </summary>
        private void AddDirectSegment(List<Coordinate> lineCoordinates, StationData from, StationData to)
        {
            try
            {
                var fromPoint = SphericalMercator.FromLonLat(from.Longitude, from.Latitude);
                var toPoint = SphericalMercator.FromLonLat(to.Longitude, to.Latitude);

                // If this is the first point of the segment, add it
                if (lineCoordinates.Count == 0 || 
                    (lineCoordinates[lineCoordinates.Count - 1].X != fromPoint.x || 
                     lineCoordinates[lineCoordinates.Count - 1].Y != fromPoint.y))
                {
                    lineCoordinates.Add(new Coordinate(fromPoint.x, fromPoint.y));
                }

                lineCoordinates.Add(new Coordinate(toPoint.x, toPoint.y));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding direct segment: {ex.Message}");
            }
        }

        /// <summary>
        /// Set map to display entire route
        /// </summary>
        private void FitToRoute(LineString lineString)
        {
            try
            {
                var envelope = lineString.EnvelopeInternal;
                if (envelope != null)
                {
                    // Mapsui: calculate center and set zoom
                    double centerX = (envelope.MinX + envelope.MaxX) / 2.0;
                    double centerY = (envelope.MinY + envelope.MaxY) / 2.0;
                    var center = new MPoint(centerX, centerY);
                    
                    _map.Navigator.CenterOnAndZoomTo(center, 1.0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fitting to route: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear route layer
        /// </summary>
        public void Clear()
        {
            if (_routeLayer != null)
            {
                _map.Layers.Remove(_routeLayer);
                _routeLayer = null;
            }
        }
    }
}
