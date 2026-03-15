# ACO_Optimizer

A Windows desktop application for optimizing electric vehicle (EV) charging routes using the **Ant Colony Optimization (ACO)** algorithm. Built with C# / WPF (.NET Framework 4.8).

> Developed as a master's thesis project at the Silesian University of Technology, Sosnowiec.

---

## Features

- ACO-based route optimization across a network of EV charging stations in Poland
- Supports 4 connector types: Type 2 Socket, Type 2 Tethered, CHAdeMO, CCS
- Configurable ACO parameters: number of ants, iterations, α, β, ρ (evaporation rate)
- Parallel and sequential ant simulation modes
- Interactive map visualization powered by [Mapsui](https://mapsui.com/) and OpenStreetMap
- Intelligent charging decision logic (energy-based, exploration probability)
- Detailed per-station diagnostics: energy on arrival/departure, charging time, queue time
- Export of results

---

## Requirements

- Windows 10/11
- .NET Framework 4.8
- Visual Studio 2022 (Community or higher)

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/YOUR_USERNAME/ACO_Optimizer_public.git
```

### 2. Restore NuGet packages

Open `ACO_Optimizer.sln` in Visual Studio. NuGet packages will be restored automatically on first build (or via *Tools → NuGet Package Manager → Restore*).

### 3. Build and run

Set the build configuration to `Debug` or `Release` and press **F5**.

---

## Data Files

Located in the `Data/` folder:

| File | Description |
|------|-------------|
| `stations.json` | 50 EV charging stations in Poland with coordinates and connector types |
| `distance-matrix.json` | Pre-calculated road distances between all station pairs |
| `routes.jsonl.gz` | Compressed NDJSON with full route geometry (via OSRM) |
| `osrm-data-updater.ps1` | PowerShell script to refresh distance matrix and routes from OSRM |
| `stations-json-updater.ps1` | PowerShell script to update charger power data from OpenChargeMap API |

### Updating data

**Distance matrix / routes** — uses the public [OSRM demo server](https://router.project-osrm.org), no API key needed:
```powershell
cd Data
.\osrm-data-updater.ps1
```

**Charging station data** — requires a free [OpenChargeMap API key](https://openchargemap.org/site/developerinfo). Edit `stations-json-updater.ps1` and replace `YOUR_OCM_API_KEY_HERE`:
```powershell
cd Data
.\stations-json-updater.ps1
```

---

## Project Structure

```
ACO_Optimizer/
├── Models/          # Domain models: Station, Route, Ant, Graph, ACOParameters
├── Services/        # ACO algorithm, data loading, OSRM integration, export
├── ViewModels/      # MVVM view models (MainViewModel, SettingsViewModel)
├── Views/           # WPF XAML views
├── Converters/      # WPF value converters
├── Helpers/         # Utility classes (MapsuiUtils, RelayCommand)
├── UI/              # Route renderer (map visualization)
├── Resources/       # XAML styles
├── Tests/           # Integration tests
└── Data/            # Station data, distance matrix, route cache, update scripts
```

---

## Algorithm Overview

The optimizer implements ACO adapted for the EV routing problem:

1. **Initialization** — pheromone matrix set to uniform values
2. **Iteration loop** — each iteration constructs `N` ant solutions:
   - Each ant starts at the source station and selects the next station probabilistically based on pheromone level (α) and visibility (β, inverse of estimated travel+charging time)
   - Charging decisions are made based on reachability and an exploration probability
3. **Pheromone update** — evaporation (ρ) followed by deposition proportional to `1 / TotalTime` of each completed route
4. **Best solution tracking** — best route minimizes total travel time (including charging and queue times)

Both **sequential** and **parallel** ant execution modes are available. Sequential mode produces better solutions due to consistent pheromone state; parallel mode is significantly faster.

---

## License

MIT License — see [LICENSE](LICENSE) for details.

© 2026 Filip Łada
