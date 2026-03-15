# ACO_Optimizer - Testing Instructions

## Overview
This solution contains integration tests for the ACO (Ant Colony Optimization) algorithm with OSRM (Open Source Routing Machine) data.

## Running Tests

### Prerequisites
1. Visual Studio 2019+ with .NET Framework 4.8
2. Required NuGet packages (automatically restored):
   - Newtonsoft.Json
   - NetTopologySuite
   - Mapsui
   - All dependencies in packages.config

### Methods

#### Method 1: Using Visual Studio Test Explorer (Recommended)

Currently, the test class `OsrmIntegrationTests` contains three public static test methods that can be executed manually through the Immediate Window or by creating a test runner console application.

**To run tests manually in Visual Studio:**

1. Open `Tests/OsrmIntegrationTests.cs`
2. You can either:
   - Copy the test method code to the Immediate Window (Debug > Windows > Immediate)
   - Or create a simple console application that calls these methods

**Example:** Create `Program.cs` in a console app:
```csharp
using System;
using ACO_Optimizer.Tests;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting ACO_Optimizer Integration Tests...\n");
        
        OsrmIntegrationTests.TestDistanceMatrixProvider();
        OsrmIntegrationTests.TestRouteRepository();
        OsrmIntegrationTests.TestACOWithOSRM();
        
        Console.WriteLine("\n? All tests completed");
        Console.ReadKey();
    }
}
```

#### Method 2: Using xUnit or NUnit (Recommended for Production)

To properly integrate tests, install a testing framework:

```powershell
# In Package Manager Console
Install-Package xunit
Install-Package xunit.runner.visualstudio
```

Then convert `OsrmIntegrationTests.cs` to use attributes:

```csharp
[Fact]
public void TestDistanceMatrixProvider() { ... }

[Fact]
public void TestRouteRepository() { ... }

[Fact]
public void TestACOWithOSRM() { ... }
```

### Test Descriptions

#### Test 1: Distance Matrix Provider
- **File:** `Services/DistanceMatrixProvider.cs`
- **What it tests:** Loading and accessing distance matrix from `distance-matrix.json`
- **Expected result:** Successfully loads matrix and retrieves distances between stations
- **Run time:** < 1 second

#### Test 2: Route Repository
- **File:** `Services/RouteRepositoryGzip.cs`
- **What it tests:** Loading compressed route geometries from `routes.jsonl.gz`
- **Expected result:** Successfully loads route index and retrieves route edges
- **Run time:** 2-5 seconds (due to file decompression)

#### Test 3: ACO with OSRM Integration
- **File:** `Services/ACOService.cs`
- **What it tests:** Full integration of ACO algorithm with OSRM data services
- **Expected result:** Runs mini ACO with 3 iterations and 5 ants, finds a valid route
- **Run time:** 5-15 seconds (algorithm execution)

### Required Data Files

Tests require these files in the `Data/` folder (relative to executable):

```
Data/
??? stations.json           (Station definitions with coordinates)
??? distance-matrix.json    (Pre-computed distance matrix)
??? routes.jsonl.gz         (Compressed route geometries)
```

**Note:** These files are NOT included in the repository. Ensure they are present before running tests.

### Expected Output

Successful test run output:

```
=== Test 1: Distance Matrix Provider ===
Distance [0]->[1]: 245.5 km
Invalid index result:  (should be null)
? Distance Matrix Provider test passed

=== Test 2: Route Repository ===
Edge [0]->[1]: 245.5 km, 8920 sec
Geometry points: 342
First point: (19.9450, 50.0646)
? Route Repository test passed

=== Test 3: ACO with OSRM ===
Loaded 999 stations
? Route found: 45 stations, 2345.67 km
```

### Troubleshooting

#### "SKIP: Matrix file not found"
- Ensure `Data/distance-matrix.json` exists
- Check file path is correct relative to executable location

#### "SKIP: Routes file not found"
- Ensure `Data/routes.jsonl.gz` exists and is not corrupted
- Try re-downloading the file

#### "Station not found in OSRM data"
- Ensure `Data/stations.json` matches stations used in distance matrix
- Check JSON format is correct

#### Build Fails
1. Restore NuGet packages: `dotnet restore`
2. Rebuild solution: `Rebuild Solution`
3. Check all .csproj file references

### Continuous Integration

For CI/CD pipelines, create a test runner executable in the solution:

```xml
<ItemGroup>
    <ProjectReference Include="..\ACO_Optimizer\ACO_Optimizer.csproj" />
</ItemGroup>
```

Then build and run:
```bash
msbuild ACO_Optimizer.sln
dotnet test
```
