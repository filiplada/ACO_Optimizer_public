
<#
  stations-json-updater.ps1
  Aktualizacja pól "ChargerTypes" w stations.json na podstawie mocy (kW) z OCM.
  Metryka: mediana mocy dla typu złącza w promieniu RADIUS_KM od miasta.
  ZMIANA: jeśli dla danego typu brak danych z OCM => typ jest USUWANY z ChargerTypes.
#>

# ==== KONFIGURACJA ====
# Klucz API OpenChargeMap – uzyskaj bezpłatnie na https://openchargemap.org/site/developerinfo
$ApiKey        = "YOUR_OCM_API_KEY_HERE"
$InputPath     = "stations.json"
$OutputPath    = "stations.updated.json"
$RadiusKm      = 5
$MaxResults    = 2000
$CountryCode   = "PL"
$ThrottleMs    = 700

# Większy promień dla dużych miast (dopasuj do swoich nazw z stations.json)
$CityRadiusOverrides = @{
    "Warszawa" = 12
    "Kraków"   = 10
    "Wrocław"  = 10
    "Poznań"   = 10
    "Gdańsk"   = 10
    "Gdynia"   = 10
}

# Mapowanie nazw w pliku -> OCM ConnectionTypeID
# (OCM referencedata: Type 2 Socket=25, Type 2 Tethered=1036, CHAdeMO=2, CCS Type 2=33)
$ConnectionTypeIds = @{
    "Type 2 (Socket Only)"        = 25
    "Type 2 (Tethered Connector)" = 1036
    "CHAdeMO"                     = 2
    "CCS (Type 2)"                = 33
}

# ==== FUNKCJE ====

function Get-Median {
    param([double[]]$Values)
    if (-not $Values -or $Values.Count -eq 0) { return $null }
    $sorted = $Values | Sort-Object
    $n = $sorted.Count
    if ($n -eq 1) { return [Math]::Round([double]$sorted[0], 2) }
    if ($n % 2 -eq 1) {
        $idx = [int][Math]::Floor($n / 2)
        return [Math]::Round([double]$sorted[$idx], 2)
    } else {
        $mid = $n / 2
        $m = (([double]$sorted[$mid - 1]) + ([double]$sorted[$mid])) / 2.0
        return [Math]::Round($m, 2)
    }
}

function Get-EstimatedPowerKW {
    param([object]$Conn)
    if ($Conn.PowerKW) { return [double]$Conn.PowerKW }
    $amps = $Conn.Amps
    $volt = $Conn.Voltage
    $ctype = $Conn.CurrentTypeID  # 10=AC 1-faza, 20=AC 3-fazy, 30=DC
    if ($amps -and $volt) {
        if ($ctype -eq 20) { return [Math]::Round(([Math]::Sqrt(3) * $volt * $amps) / 1000.0, 2) }
        return [Math]::Round(($volt * $amps) / 1000.0, 2)
    }
    return $null
}

function Get-OCMPOI {
    param([double]$Latitude,[double]$Longitude,[int]$RadiusKmLocal)
    $uri = "https://api.openchargemap.io/v3/poi/`?output=json&compact=true&verbose=false" +
           "&latitude=$Latitude&longitude=$Longitude" +
           "&distance=$RadiusKmLocal&distanceunit=KM" +
           "&countrycode=$CountryCode&maxresults=$MaxResults"
    $headers = @{ "X-API-Key" = $ApiKey; "User-Agent" = "stations-json-updater-ps/1.0" }
    try   { return Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 60 }
    catch { Write-Warning ("Błąd pobierania OCM: {0}" -f $_.Exception.Message); return @() }
}

function Get-MedianPowerForType {
    param([object[]]$Pois,[int]$ConnectionTypeId)
    $powers = New-Object System.Collections.Generic.List[Double]
    foreach ($poi in ($Pois | Where-Object { $_ })) {
        $conns = $poi.Connections
        if (-not $conns) { continue }
        foreach ($conn in $conns) {
            if ($conn.ConnectionTypeID -eq $ConnectionTypeId) {
                $pkw = Get-EstimatedPowerKW -Conn $conn
                if ($null -ne $pkw -and $pkw -gt 0) { [void]$powers.Add([double]$pkw) }
            }
        }
    }
    if ($powers.Count -eq 0) { return $null }
    return Get-Median -Values $powers.ToArray()
}

# ==== GŁÓWNY PRZEBIEG ====

if (-not (Test-Path -Path $InputPath)) {
    Write-Error "Nie znaleziono pliku: $InputPath"; exit 1
}

Write-Host ("Wczytywanie {0}..." -f $InputPath)
$stationsJson = Get-Content -Path $InputPath -Raw
$stations = $stationsJson | ConvertFrom-Json

foreach ($s in $stations) {
    $name = $s.Name
    $lat  = [double]$s.Latitude
    $lon  = [double]$s.Longitude

    $radius = $RadiusKm
    if ($CityRadiusOverrides.ContainsKey($name)) { $radius = $CityRadiusOverrides[$name] }

    Write-Host ("{0}: pobieram OCM w promieniu {1} km..." -f $name, $radius)
    $pois = Get-OCMPOI -Latitude $lat -Longitude $lon -RadiusKmLocal $radius
    if (-not $pois -or $pois.Count -eq 0) {
        Write-Warning ("Brak POI z OCM dla {0} (ChargerTypes pozostaje bez zmian w tym mieście)." -f $name)
        Start-Sleep -Milliseconds $ThrottleMs
        continue
    }

    if (-not $s.ChargerTypes) { $s.ChargerTypes = [pscustomobject]@{} }

    $updatedCT = @{}

    # Iteruj po właściwościach (kluczach) istniejącego obiektu ChargerTypes
    foreach ($prop in $s.ChargerTypes.PSObject.Properties) {
        $label = $prop.Name
        $currentVal = $prop.Value

        if ($ConnectionTypeIds.ContainsKey($label)) {
            $ctid = $ConnectionTypeIds[$label]
            $median = Get-MedianPowerForType -Pois $pois -ConnectionTypeId $ctid
            if ($null -ne $median) {
                # aktualizujemy tylko gdy mamy dane
                $updatedCT[$label] = [double]$median
                Write-Host ("   • {0} => {1} kW (mediana)" -f $label, $median)
            } else {
                # BRAK danych: USUŃ ten typ (nie kopiuj go do updatedCT)
                Write-Host ("   • {0} => brak danych w OCM: usuwam z ChargerTypes" -f $label)
            }
        } else {
            # typ bez mapowania -> przenosimy jak jest (jeśli chcesz też usuwać, usuń ten blok)
            $num = $currentVal
            if ($num -is [string]) {
                [double]$tryVal = 0
                if ([double]::TryParse($num, [ref]$tryVal)) { $num = $tryVal }
            }
            $updatedCT[$label] = $num
            Write-Host ("   • {0} => brak mapowania: pozostawiam" -f $label)
        }
    }

    # Złóż z powrotem PSCustomObject z uaktualnionymi (i ewentualnie przerzedzonymi) właściwościami
    $s.ChargerTypes = [pscustomobject]@{}
    foreach ($k in $updatedCT.Keys) {
        Add-Member -InputObject $s.ChargerTypes -NotePropertyName $k -NotePropertyValue $updatedCT[$k] -Force
    }

    Start-Sleep -Milliseconds $ThrottleMs
}

Write-Host ("Zapis do {0}..." -f $OutputPath)
$stations | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host ("Gotowe: {0}" -f $OutputPath)
