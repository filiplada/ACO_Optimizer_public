#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$StationsPath = ".\stations.json",
    [string]$MatrixPath   = ".\distance-matrix.json",
    [string]$RoutesPath   = ".\routes.jsonl.gz",
    [string]$OsrmBaseUrl  = "https://router.project-osrm.org",
    [ValidateSet("driving","walking","cycling")]
    [string]$Profile      = "driving",
    [int]$DestBlockSize   = 25,
    [int]$PauseTableMs    = 150,
    [int]$PauseRouteMs    = 120,
    [switch]$SkipDiagonal
)

$ErrorActionPreference = "Stop"

# --- Culture: zawsze kropki ---
$invar = [System.Globalization.CultureInfo]::InvariantCulture

# --- TLS 1.2 (bez wyłączania innych) ---
try {
    $sp = [Net.ServicePointManager]::SecurityProtocol
    if ($sp.ToString() -notmatch 'Tls12') {
        [Net.ServicePointManager]::SecurityProtocol = $sp -bor [Net.SecurityProtocolType]::Tls12
    }
} catch {}

# --- Proxy (jeśli w środowisku jest) ---
try { [System.Net.WebRequest]::DefaultWebProxy.Credentials = [System.Net.CredentialCache]::DefaultNetworkCredentials } catch {}

# --- Pomocnicze: formaty/liczby ---
function Format-Decimal6([double]$x) { $x.ToString('0.000000', $invar) }
function Format-LonLat([double]$lon, [double]$lat) { '{0},{1}' -f (Format-Decimal6 $lon), (Format-Decimal6 $lat) }
function Assert-CoordsString([string]$coords, [string]$label) {
    if ($coords -notmatch '^[0-9\-\.\,;]+$') { throw "[$label] Niedozwolone znaki w łańcuchu współrzędnych." }
    if ($coords -match '\s') { throw "[$label] Białe znaki (spacje/taby) są niedozwolone." }
}

# --- Budowanie URL-i BEZ interpolacji (PS 5.1-safe) ---
function New-OsrmRouteUrl([string]$base,[string]$profile,[double]$lon1,[double]$lat1,[double]$lon2,[double]$lat2,[switch]$GeoJson) {
    $pair = '{0};{1}' -f (Format-LonLat $lon1 $lat1), (Format-LonLat $lon2 $lat2)
    $q = if ($GeoJson) { 'overview=full&geometries=geojson&steps=false&alternatives=false' } else { 'overview=false&steps=false&alternatives=false' }
    '{0}/route/v1/{1}/{2}?{3}' -f $base, $profile, $pair, $q
}
function New-OsrmTableUrl([string]$base,[string]$profile,[string]$coords,[int]$source,[int[]]$destIdx) {
    $destParam = ($destIdx -join ';')
    '{0}/table/v1/{1}/{2}?annotations=distance&sources={3}&destinations={4}' -f $base, $profile, $coords, $source, $destParam
}

Write-Host "=== OSRM matrix & routes ==="
Write-Host "Stations   : $StationsPath"
Write-Host "Matrix (km): $MatrixPath"
Write-Host "Routes (gz): $RoutesPath"
Write-Host "OSRM host  : $OsrmBaseUrl  (profile=$Profile)"
Write-Host "Block size : $DestBlockSize"
Write-Host "Pauses(ms) : table=$PauseTableMs, route=$PauseRouteMs"

# 1) Wczytaj stacje
if (-not (Test-Path -LiteralPath $StationsPath)) { throw "Nie znaleziono pliku $StationsPath" }
$raw = Get-Content -LiteralPath $StationsPath -Raw
if ([string]::IsNullOrWhiteSpace($raw)) { throw "Plik $StationsPath jest pusty." }
$stations = $raw | ConvertFrom-Json
if (-not $stations) { throw "Nie udało się sparsować $StationsPath (JSON)." }

$points = for ($i=0; $i -lt $stations.Count; $i++) {
    $s = $stations[$i]
    [pscustomobject]@{
        Index     = $i
        Id        = $s.Id
        Name      = $s.Name
        Latitude  = [double]$s.Latitude
        Longitude = [double]$s.Longitude
    }
}
$N = $points.Count
if ($N -lt 2) { throw "Potrzeba co najmniej 2 stacji (N=$N)." }

# 2) Zbuduj coordPath (lon,lat;lon,lat;...)
$coordPath = ($points | ForEach-Object { Format-LonLat $_.Longitude $_.Latitude }) -join ';'
Assert-CoordsString -coords $coordPath -label "coordPath"
Write-Host ("coordPath length: {0} characters" -f $coordPath.Length)
Write-Host ("Sample[0]: {0}" -f (Format-LonLat $points[0].Longitude $points[0].Latitude))
if ($N -gt 1) { Write-Host ("Sample[1]: {0}" -f (Format-LonLat $points[1].Longitude $points[1].Latitude)) }

# 3) Test łączności + fallback HTTPS -> HTTP
$testUrl = New-OsrmRouteUrl -base $OsrmBaseUrl -profile $Profile `
            -lon1 $points[0].Longitude -lat1 $points[0].Latitude `
            -lon2 $points[1].Longitude -lat2 $points[1].Latitude
Write-Host "Connectivity test: $testUrl"

$ok = $false
try {
    $r = Invoke-RestMethod -Method GET -Uri $testUrl -TimeoutSec 60 -MaximumRedirection 5
    if (-not $r.code -or $r.code -eq 'Ok') { $ok = $true }
} catch {
    Write-Warning $_.Exception.Message
}
if (-not $ok) {
    if ($OsrmBaseUrl -like 'https://*') {
        $httpBase = $OsrmBaseUrl -replace '^https://','http://'
        $testUrl2 = New-OsrmRouteUrl -base $httpBase -profile $Profile `
                    -lon1 $points[0].Longitude -lat1 $points[0].Latitude `
                    -lon2 $points[1].Longitude -lat2 $points[1].Latitude
        Write-Warning "HTTPS failed -> retry over HTTP"
        Write-Host "Connectivity test: $testUrl2"
        try {
            $r2 = Invoke-RestMethod -Method GET -Uri $testUrl2 -TimeoutSec 60 -MaximumRedirection 5
            if (-not $r2.code -or $r2.code -eq 'Ok') { $OsrmBaseUrl = $httpBase; $ok = $true }
        } catch { Write-Warning $_.Exception.Message }
    }
    if (-not $ok) { throw "OSRM connectivity failed for $OsrmBaseUrl" }
}

# 4) Otwórz strumienie wyjściowe
$matrixFs  = [System.IO.File]::Create($MatrixPath)
$matrixSw  = New-Object System.IO.StreamWriter($matrixFs, (New-Object System.Text.UTF8Encoding($false))); $matrixSw.AutoFlush = $true
$routesFs  = [System.IO.File]::Create($RoutesPath)
$gzip      = New-Object System.IO.Compression.GZipStream($routesFs, [System.IO.Compression.CompressionMode]::Compress)
$routesSw  = New-Object System.IO.StreamWriter($gzip, (New-Object System.Text.UTF8Encoding($false))); $routesSw.AutoFlush = $true

# Nagłówek macierzy
$matrixSw.WriteLine("{")
$matrixSw.WriteLine('  "unit": "kilometers",')
$matrixSw.WriteLine("  ""size"": $N,")
$matrixSw.WriteLine('  "distances": [')

# 5) Pętle
for ($i = 0; $i -lt $N; $i++) {

    Write-Host ("[MATRIX] Row {0}/{1}" -f ($i+1), $N)
    Write-Progress -Activity "Matrix rows" -Status "Row $i" -PercentComplete (100*$i/$N)

    $rowKm = New-Object 'System.Object[]' $N  # pozwoli trzymać $null
    $rowKm[$i] = 0.0

    # 5a) /table – partiami po kolumnach
    for ($blk = 0; $blk -lt $N; $blk += $DestBlockSize) {
        $end = [Math]::Min($blk + $DestBlockSize - 1, $N-1)
        $dstIdxs = $blk..$end

        $tableUrl = New-OsrmTableUrl -base $OsrmBaseUrl -profile $Profile -coords $coordPath -source $i -destIdx $dstIdxs
        if ($tableUrl -match '&amp;') { throw "W URL znalazł się HTML-escaped '&amp;'. Używaj zwykłego '&'." }

        try {
            $data = Invoke-RestMethod -Method GET -Uri $tableUrl -TimeoutSec 60 -MaximumRedirection 5
        } catch {
            Write-Warning "Błąd /table dla i=$i, blk=$blk..$end"
            Write-Warning "URL snippet: $($tableUrl.Substring(0, [Math]::Min($tableUrl.Length, 280)))..."
            throw
        }

        if ($data -and $data.distances -and $data.distances.Count -gt 0) {
            for ($k=0; $k -lt $dstIdxs.Count; $k++) {
                $m = $data.distances[0][$k]
                if ($m -ne $null) { $rowKm[$dstIdxs[$k]] = [Math]::Round(($m / 1000.0), 6) }
            }
        }
        Start-Sleep -Milliseconds $PauseTableMs
    }

    # 5b) /route – NDJSON + uzupełnianie NULL z /table
    for ($j=0; $j -lt $N; $j++) {
        if ($SkipDiagonal -and $i -eq $j) { continue }
        Write-Progress -Activity "Routes" -Status "route $i->$j" -PercentComplete (100.0 * (($i*$N)+$j+1) / ($N*$N))

        $pFrom = $points[$i]; $pTo = $points[$j]
        $routeUrl = New-OsrmRouteUrl -base $OsrmBaseUrl -profile $Profile -lon1 $pFrom.Longitude -lat1 $pFrom.Latitude -lon2 $pTo.Longitude -lat2 $pTo.Latitude -GeoJson

        try {
            $rd = Invoke-RestMethod -Uri $routeUrl -TimeoutSec 60 -MaximumRedirection 5
        } catch {
            Write-Warning "Błąd /route dla $i->$j"
            Write-Warning "URL: $routeUrl"
            throw
        }
        if (-not $rd.routes -or $rd.routes.Count -eq 0) { throw "OSRM /route nie zwróciło żadnej trasy dla $i->$j." }

        $rt = $rd.routes[0]
        $distanceKm = [math]::Round($rt.distance/1000.0, 6)
        $durationS  = [math]::Round($rt.duration, 1)

        $obj = [pscustomobject]@{
            fromIndex  = $i
            toIndex    = $j
            fromId     = $pFrom.Id
            toId       = $pTo.Id
            distanceKm = $distanceKm
            durationS  = $durationS
            geometry   = $rt.geometry
        }
        $routesSw.WriteLine(($obj | ConvertTo-Json -Depth 6 -Compress))

        if ($null -eq $rowKm[$j]) { $rowKm[$j] = $distanceKm }

        Start-Sleep -Milliseconds $PauseRouteMs
    }

    # 5c) Zapisz wiersz macierzy
    if ($i -gt 0) { $matrixSw.WriteLine(",") }
    $matrixSw.Write("    [")
    for ($c=0; $c -lt $N; $c++) {
        if ($c -gt 0) { $matrixSw.Write(",") }
        $val = $rowKm[$c]
        if ($null -eq $val) {
            $matrixSw.Write("null")
        } else {
            $matrixSw.Write( (Format-Decimal6 $val) )
        }
    }
    $matrixSw.Write("]")
}

# 6) Domknięcie plików
$matrixSw.WriteLine()
$matrixSw.WriteLine("  ]")
$matrixSw.WriteLine("}")
$matrixSw.Flush(); $matrixSw.Dispose(); $matrixFs.Dispose()
$routesSw.Flush(); $routesSw.Dispose(); $gzip.Dispose(); $routesFs.Dispose()

Write-Host "=== Done ==="
Write-Host "Matrix  : $MatrixPath"
Write-Host "Routes  : $RoutesPath"