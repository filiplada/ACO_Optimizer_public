using System;
using System.Windows;
using ACO_Optimizer.Services;

namespace ACO_Optimizer
{
    /// <summary>
    /// Logika interakcji dla klasy App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Zainicjalizuj OSRM na starcie aplikacji
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                RoadNetworkService.Initialize(
                    System.IO.Path.Combine(baseDir, "Data", "stations.json"),
                    System.IO.Path.Combine(baseDir, "Data", "distance-matrix.json"),
                    System.IO.Path.Combine(baseDir, "Data", "routes.jsonl.gz")
                );

                System.Diagnostics.Debug.WriteLine("✓ OSRM initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to initialize OSRM: {ex.Message}");
                MessageBox.Show(
                    $"Błąd inicjalizacji OSRM:\n{ex.Message}\n\nUpewnij się że pliki znajdują się w folderze Data/",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

