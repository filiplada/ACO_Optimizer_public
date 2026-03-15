using System.Collections.Generic;
using System.Linq;
using ACO_Optimizer.Models;

public class Ant
{
    public List<Station> Path { get; private set; }
    public double TotalDistance { get; private set; }
    public double TotalTime { get; private set; }

    private double batteryLevel;
    private readonly double batteryCapacity;
    private readonly double consumptionPerKm;
    private readonly bool onlyPricedChargers;
    private readonly string chargerType;

    public Ant(double initialBattery, double batteryCapacity, double consumptionPerKm, bool onlyPricedChargers, string chargerType)
    {
        Path = new List<Station>();
        TotalDistance = 0;
        TotalTime = 0;
        this.batteryLevel = initialBattery;
        this.batteryCapacity = batteryCapacity;
        this.consumptionPerKm = consumptionPerKm;
        this.onlyPricedChargers = onlyPricedChargers;
        this.chargerType = chargerType;
    }

    // Expose remaining battery so ACOService can check feasibility
    public double RemainingBattery => batteryLevel;

    // Can the ant travel the given distance with current battery?
    public bool CanReach(double distance)
    {
        return batteryLevel >= distance * consumptionPerKm;
    }

    // Visit assumes caller ensured the ant can reach the station.
    // Throws InvalidOperationException if called with unreachable distance.
    public void Visit(Station station, double distance, bool allowCharging = true)
    {
        // Validate reachability
        if (!CanReach(distance))
            throw new System.InvalidOperationException("Insufficient battery to reach the station.");

        // Create a simulation copy so the ant records diagnostics on its own Station instance
        // and does not overwrite the shared graph Station properties used by other ants.
        var simStation = station.CreateSimulationCopy();

        Path.Add(simStation);
        TotalDistance += distance;

        // Travel time (90 km/h ~ 0.66 min/km)
        double travelTime = distance * 0.66;
        TotalTime += travelTime;

        // Consume energy for the traveled distance
        double energyConsumed = distance * consumptionPerKm;
        batteryLevel -= energyConsumed;

        // RECORD: energy on arrival (before any charging at this station)
        simStation.EnergyOnArrivalKwh = batteryLevel;

        // Compute how much can be replenished (top-up to capacity)
        double maxReplenishable = batteryCapacity - batteryLevel;
        double energyToReplenish = maxReplenishable; // keep topping model as before

        double chargeTime = 0.0;
        bool charged = false;
        bool canCharge = onlyPricedChargers ? simStation.IsPricedCharger : true;

        // Determine available charger power for the selected charger type (if present)
        double selectedChargerPowerKw = 0.0;
        if (simStation.ChargerTypes != null && !string.IsNullOrEmpty(chargerType))
        {
            simStation.ChargerTypes.TryGetValue(chargerType, out selectedChargerPowerKw);
        }

        // Only perform charging if allowed, charger exists and power > 0, and charging policy permits
        if (allowCharging && selectedChargerPowerKw > 0 && energyToReplenish > 0 && canCharge)
        {
            chargeTime = (energyToReplenish / selectedChargerPowerKw) * 60.0; // minutes
            batteryLevel += energyToReplenish;
            if (batteryLevel > batteryCapacity) batteryLevel = batteryCapacity;
            simStation.ChargingTimeMin = chargeTime;
            charged = true;
            simStation.WasChargedHere = true;
        }
        else
        {
            // no charging happened
            simStation.ChargingTimeMin = 0;
            simStation.WasChargedHere = false;
        }

        // Record energy on departure (kWh) for this station
        simStation.EnergyOnDepartureKwh = batteryLevel;

        // Add charging time and queue time only if charging occurred
        TotalTime += chargeTime;
        if (charged)
            TotalTime += simStation.QueueTimeMin;
    }

    public bool HasVisited(string stationId)
    {
        return Path.Exists(s => s.Id == stationId);
    }
}