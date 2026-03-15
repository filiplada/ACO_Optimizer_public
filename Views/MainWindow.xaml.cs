using ACO_Optimizer.Helpers;
using ACO_Optimizer.Models;
using ACO_Optimizer.Services;
using ACO_Optimizer.ViewModels;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Wpf;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;


namespace ACO_Optimizer.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Mapsui.Map _map;
        private MemoryLayer _stationsLayer;
        private MemoryLayer _routeLayer;
        public List<Coordinate> _coordinates = new List<Coordinate>();
        MemoryLayer _pointsLayer = new MemoryLayer()
        {
            Name = "Points",
            Style = CreateBitmapStyle(),
            Features = new List<IFeature>()
        };

        public MainWindow()
        {
            InitializeComponent();
            var mvm  = new MainViewModel();
            DataContext = mvm;
            startStations.ItemsSource = mvm.Stations;

            LoadMap();

            // Subskrybuj zmiany trasy
            mvm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(mvm.BestRoute))
                {
                    // Only update map visualization if EnableVisualMapUpdate is true
                    if (mvm.Parameters.EnableVisualMapUpdate)
                    {
                        UpdateRouteLayer(mvm.BestRoute);
                    }
                }
            };

        }


        private void LoadMap()
        {
            // Ustawienie providerów mapy (OpenStreetMap)
            _map = new Mapsui.Map { CRS = "EPSG:3857" };
            _map.Layers.Add(OpenStreetMap.CreateTileLayer());

            // Ustawienie pozycji (Warszawa)
            var center = SphericalMercator.FromLonLat(21.0122, 52.2297).ToMPoint();
            _map.Home = n => n.CenterOnAndZoomTo(center, n.Resolutions[10]);
            _map.Layers.Add(_pointsLayer);

            MapControl.Map = _map;

            AddStationsLayer();
        }

        private void AddStationsLayer()
        {
            var vm = (MainViewModel)DataContext;
            foreach (var station in vm.Stations)
            {
                //_coordinates.Add(new Coordinate(station.Longitude, station.Latitude));

                var feature = new PointFeature(
            SphericalMercator.FromLonLat(station.Longitude, station.Latitude).ToMPoint()
            );
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 0.4,
                    Fill = new Brush(Color.YellowGreen),
                    Outline = new Pen(Color.Black, 1)
                });
                ((List<IFeature>)_pointsLayer.Features).Add(feature);
            }
            ;
                
            MapControl.RefreshGraphics();
        }

        private void UpdateRouteLayer(Route bestRoute)
        {
            _coordinates.Clear();
            var lineLayer = MapControl.Map.Layers.FindLayer("Line Layer").FirstOrDefault();

            if (lineLayer != null)
            {
                MapControl.Map.Layers.Remove(lineLayer);
            }

            if (bestRoute == null || bestRoute.Stations == null || !bestRoute.Stations.Any())
            {
                return;
            }

            // Pobierz rzeczywiste trasy z OSRM (z geometrią!)
            var routeRepository = RoadNetworkService.GetRouteRepository();
            var osrmStations = RoadNetworkService.GetStations();
            
            if (routeRepository == null || osrmStations == null || osrmStations.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠ OSRM data not available for route rendering");
                return;
            }

            var allCoordinates = new List<Coordinate>();

            // Dla każdej pary kolejnych stacji w trasie
            for (int i = 0; i < bestRoute.Stations.Count - 1; i++)
            {
                var currentStation = bestRoute.Stations[i];
                var nextStation = bestRoute.Stations[i + 1];

                // Znajdź indeksy w OSRM
                var fromData = osrmStations.FirstOrDefault(s => s.Id == currentStation.Id);
                var toData = osrmStations.FirstOrDefault(s => s.Id == nextStation.Id);

                if (fromData == null || toData == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠ Station not found in OSRM: {currentStation.Id} or {nextStation.Id}");
                    continue;
                }

                // Pobierz rzeczywistą trasę z OSRM (z geometrią!)
                var edge = routeRepository.GetEdgeByIndex(fromData.Index, toData.Index, withGeometry: true);

                if (edge != null && edge.GeometryPoints.Count > 0)
                {
                    // Mamy rzeczywistą trasę! Dodaj wszystkie punkty
                    foreach (var (lon, lat) in edge.GeometryPoints)
                    {
                        allCoordinates.Add(new Coordinate(lon, lat));
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"✓ Route segment {currentStation.Id} → {nextStation.Id}: {edge.GeometryPoints.Count} points, {edge.DistanceKm:F2} km");
                }
                else
                {
                    // Fallback nie powinien się tu pojawić, ale dla bezpieczeństwa
                    System.Diagnostics.Debug.WriteLine(
                        $"✗ No geometry for {fromData.Id} → {toData.Id}");
                    allCoordinates.Add(new Coordinate(currentStation.Longitude, currentStation.Latitude));
                    allCoordinates.Add(new Coordinate(nextStation.Longitude, nextStation.Latitude));
                }
            }

            // Dodaj ostatnią stację
            if (bestRoute.Stations.Count > 0)
            {
                var lastStation = bestRoute.Stations[bestRoute.Stations.Count - 1];
                allCoordinates.Add(new Coordinate(lastStation.Longitude, lastStation.Latitude));
            }

            if (allCoordinates.Count < 2)
            {
                System.Diagnostics.Debug.WriteLine("✗ Not enough points to draw route");
                return;
            }

            // Rysuj rzeczywistą trasę
            Color color = Color.Red;  // Czerwona dla lepszej widoczności
            ICollection<IStyle> styles = new List<IStyle>
            {
                MapsuiUtils.GetRoundedLineStyle(5, Color.Black),      // Obramowanie
                MapsuiUtils.GetSquaredLineStyle(3, color),            // Główna linia
            };

            LineString lineString = MapsuiUtils.CreateLineString(allCoordinates);
            MapControl.Map.Layers.Add(MapsuiUtils.CreateLinestringLayer(lineString, "Line Layer", styles));

            System.Diagnostics.Debug.WriteLine(
                $"✓ Route rendered: {allCoordinates.Count} points, {bestRoute.Stations.Count} stations");
        }

        static SymbolStyle CreateBitmapStyle()
        {
            var bitmapId = GetBitmapIdForEmbeddedResource(@"DemoMapsui.Assets.gps.png");
            var bitmapHeight = 200;
            return new SymbolStyle
            {
                BitmapId = bitmapId,
                SymbolScale = 0.2,
                SymbolOffset = new Offset(0, bitmapHeight * 0.5)
            };
        }

        public static int GetBitmapIdForEmbeddedResource(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);

                var bitmapId = BitmapRegistry.Instance.Register(memoryStream);

                return bitmapId;
            }

            return -1;
        }
    }
}