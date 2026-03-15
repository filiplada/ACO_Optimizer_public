using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using ACO_Optimizer.Models;

namespace ACO_Optimizer.Services
{
    public class ACOService
    {
        private readonly Graph _graph;
        private readonly ACOParameters _parameters;
        private Dictionary<(string, string), double> _pheromones;
        private Random _random = new Random();
        private readonly object _pheromoneUpdateLock = new object();
        private readonly object _randomLock = new object();

        // Probability that an ant will choose to charge even when it could reach the next station.
        // This introduces exploration so ants sometimes charge earlier and avoid dead-ends on long routes.
        private readonly double _chargeExplorationProbability;

        public ACOService(Graph graph, ACOParameters parameters)
        {
            _graph = graph;
            _parameters = parameters;
            _chargeExplorationProbability = parameters.ChargeExplorationProbability;
            InitializePheromones();
        }

        // Set up initial pheromone levels on all edges
        private void InitializePheromones()
        {
            _pheromones = new Dictionary<(string, string), double>();
            foreach (var from in _graph.Stations)
            {
                foreach (var to in _graph.Stations)
                {
                    if (from.Id != to.Id)
                        _pheromones[(from.Id, to.Id)] = _parameters.InitialPheromone;
                }
            }
        }

        public Route Run(string startId)
        {
            Route bestRoute = null;

            for (int i = 0; i < _parameters.Iterations; i++)
            {
                List<Ant> ants = new List<Ant>();

                if (_parameters.EnableParallelSimulation)
                {
                    // Parallel execution of ants
                    ants = RunAntsParallel(startId);
                }
                else
                {
                    // Sequential execution of ants (original behavior)
                    ants = RunAntsSequential(startId);
                }

                // Find best route from this iteration
                // Sort by TotalTime (not TotalDistance) because in EV routing, time is what matters
                // A short distance with long charging times is worse than a longer distance with fast charging
                var lastVisited = ants
                    .Where(a => a.Path.LastOrDefault() != null && a.Path.Last().Equals(_parameters.DestinationStation))
                    .OrderBy(a => a.TotalTime)
                    .FirstOrDefault();

                if (lastVisited != null && (bestRoute == null || lastVisited.TotalTime < bestRoute.TotalTime))
                {
                    var routedStations = lastVisited.Path
                        .Select(p => new Station
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Latitude = p.Latitude,
                            Longitude = p.Longitude,
                            IsAvailable = p.IsAvailable,
                            IsPricedCharger = p.IsPricedCharger,
                            QueueTimeMin = p.QueueTimeMin,
                            ChargerTypes = p.ChargerTypes != null ? new Dictionary<string, double>(p.ChargerTypes) : null,
                            // copy diagnostic fields from the ant's per-station instance
                            EnergyOnArrivalKwh = p.EnergyOnArrivalKwh,
                            EnergyOnDepartureKwh = p.EnergyOnDepartureKwh,
                            ChargingTimeMin = p.ChargingTimeMin,
                            WasChargedHere = p.WasChargedHere
                        })
                        .ToList();

                    bestRoute = new Route
                    {
                        Stations = routedStations,
                        TotalDistance = lastVisited.TotalDistance,
                        TotalTime = lastVisited.TotalTime,
                        IterationOfBestSolution = i + 1  // 1-indexed iteration number
                    };
                }

                UpdatePheromones(ants);
            }

            return bestRoute;
        }

        /// <summary>
        /// Run ants sequentially (original behavior)
        /// </summary>
        private List<Ant> RunAntsSequential(string startId)
        {
            List<Ant> ants = new List<Ant>();

            for (int j = 0; j < _parameters.NumberOfAnts; j++)
            {
                var ant = new Ant(_parameters.InitialBattery, _parameters.BatteryCapacity, _parameters.ConsumptionPerKm, _parameters.OnlyPricedChargers, _parameters.ChargerType);
                var start = _graph.Stations.First(s => s.Id == startId);

                // Decide whether to allow charging at start using the ant's actual remaining battery on arrival.
                bool allowChargingAtStart = ShouldAllowChargingAtArrival(ant, start, ant.RemainingBattery);
                ant.Visit(start, 0, allowChargingAtStart);
                // Ensure the graph station instance is synchronized and ant.Path references it so UI bindings are consistent.
                SyncLastVisitedStationInstance(ant);

                while (ant.Path.Count < _graph.Stations.Count)
                {
                    var current = ant.Path.Last();
                    var next = SelectNextStation(ant, current);

                    if (next == null || current.Equals(_parameters.DestinationStation))
                    {
                        break;
                    }

                    var distance = _graph.GetDistance(current.Id, next.Id);

                    // Defensive: Visit may throw if caller/selection logic missed reachability.
                    try
                    {
                        // Determine energy level after arriving to 'next' (before any charging).
                        double energyAfterArrival = ant.RemainingBattery - (distance * _parameters.ConsumptionPerKm);
                        if (energyAfterArrival < 0) energyAfterArrival = 0.0;

                        // Prevent charging at destination station
                        if (_parameters.DestinationStation != null && _parameters.DestinationStation.Equals(next))
                        {
                            ant.Visit(next, distance, allowCharging: false);
                        }
                        else
                        {
                            // Decide whether to allow charging at 'next'. If from 'next' the ant can reach any
                            // remaining unvisited station or the destination without charging, prefer not to charge,
                            // but sometimes allow charging for exploration to avoid dead-ends on long routes, based on: _chargeExplorationProbability
                            bool allowCharging = ShouldAllowChargingAtArrival(ant, next, energyAfterArrival);
                            ant.Visit(next, distance, allowCharging);
                        }

                        // After Visit, synchronize the station instance back to graph and replace ant.Path reference.
                        SyncLastVisitedStationInstance(ant);
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Log and abort this ant's route construction gracefully.
                        Debug.WriteLine($"Ant cannot reach station {next.Id} from {current.Id} (distance {distance}). Aborting ant. {ex.Message}");
                        break;
                    }
                }

                ants.Add(ant);
            }

            return ants;
        }

        /// <summary>
        /// Run ants in parallel using Task Parallel Library
        /// </summary>
        private List<Ant> RunAntsParallel(string startId)
        {
            List<Ant> ants = new List<Ant>();
            object antsLock = new object();

            Parallel.For(0, _parameters.NumberOfAnts, new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Environment.ProcessorCount 
            }, 
            j =>
            {
                var ant = new Ant(_parameters.InitialBattery, _parameters.BatteryCapacity, _parameters.ConsumptionPerKm, _parameters.OnlyPricedChargers, _parameters.ChargerType);
                var start = _graph.Stations.First(s => s.Id == startId);

                // Decide whether to allow charging at start using the ant's actual remaining battery on arrival.
                bool allowChargingAtStart = ShouldAllowChargingAtArrival(ant, start, ant.RemainingBattery);
                ant.Visit(start, 0, allowChargingAtStart);
                // Do NOT sync in parallel loop - only sync after parallel execution completes

                while (ant.Path.Count < _graph.Stations.Count)
                {
                    var current = ant.Path.Last();
                    var next = SelectNextStation(ant, current);

                    if (next == null || current.Equals(_parameters.DestinationStation))
                    {
                        break;
                    }

                    var distance = _graph.GetDistance(current.Id, next.Id);

                    // Defensive: Visit may throw if caller/selection logic missed reachability.
                    try
                    {
                        // Determine energy level after arriving to 'next' (before any charging).
                        double energyAfterArrival = ant.RemainingBattery - (distance * _parameters.ConsumptionPerKm);
                        if (energyAfterArrival < 0) energyAfterArrival = 0.0;

                        // Prevent charging at destination station
                        if (_parameters.DestinationStation != null && _parameters.DestinationStation.Equals(next))
                        {
                            ant.Visit(next, distance, allowCharging: false);
                        }
                        else
                        {
                            // Decide whether to allow charging at 'next'. If from 'next' the ant can reach any
                            // remaining unvisited station or the destination without charging, prefer not to charge,
                            // but sometimes allow charging for exploration to avoid dead-ends on long routes, based on: _chargeExplorationProbability
                            bool allowCharging = ShouldAllowChargingAtArrival(ant, next, energyAfterArrival);
                            ant.Visit(next, distance, allowCharging);
                        }

                        // Do NOT sync in parallel loop - only sync after parallel execution completes
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Log and abort this ant's route construction gracefully.
                        Debug.WriteLine($"Ant cannot reach station {next.Id} from {current.Id} (distance {distance}). Aborting ant. {ex.Message}");
                        break;
                    }
                }

                lock (antsLock)
                {
                    ants.Add(ant);
                }
            });

            // Sync all ants after parallel execution completes
            foreach (var ant in ants)
            {
                SyncLastVisitedStationInstance(ant);
            }

            return ants;
        }


        private Station SelectNextStation(Ant ant, Station current)
        {
            var candidates = _graph.Stations
                .Where(s => !ant.HasVisited(s.Id)
                //&& (_parameters.MaxDistance >= _graph.GetDistance(current.Id, s.Id))
                )
                .ToList();

            if (!candidates.Any()) return null;

            var probabilities = new Dictionary<Station, double>();
            double sumOfProbabilities = 0;

            foreach (var station in candidates)
            {
                // Safe pheromone lookup (skip missing edges)
                double pheromone;
                lock (_pheromoneUpdateLock)
                {
                    if (!_pheromones.TryGetValue((current.Id, station.Id), out pheromone)) continue;
                }

                var distance = _graph.GetDistance(current.Id, station.Id);
                if (double.IsInfinity(distance) || distance == double.MaxValue) continue;
                if (!ant.CanReach(distance)) continue; // skip unreachable based on current battery
                double energyNeeded = distance * _parameters.ConsumptionPerKm;

                double travelTime = distance * 0.66; // 0.66 min/km ~ 90 km/h

                // If this candidate is the destination, don't penalize for missing charger:
                bool isDestination = _parameters.DestinationStation != null && station.Equals(_parameters.DestinationStation);

                // Determine charger power for the selected charger type (fallback to station.ChargePowerKw)
                double chargerPowerKw = 0.0;
                if (!isDestination && station.ChargerTypes != null && !string.IsNullOrEmpty(_parameters.ChargerType))
                {
                    station.ChargerTypes.TryGetValue(_parameters.ChargerType, out chargerPowerKw);
                }

                // Respect OnlyPricedChargers policy when not destination
                if (!isDestination && _parameters.OnlyPricedChargers && !station.IsPricedCharger)
                {
                    chargerPowerKw = 0.0;
                }

                double chargeTime;
                if (isDestination)
                {
                    // No charging expected at destination -> zero charge time and no queue
                    chargeTime = 0.0;
                }
                else
                {
                    if (chargerPowerKw <= 0) chargeTime = 1e6; else chargeTime = (energyNeeded / chargerPowerKw) * 60.0;
                }

                double totalCost = travelTime + chargeTime + (isDestination ? 0.0 : station.QueueTimeMin);
                double visibility = 1.0 / (totalCost + 1e-6);

                var value = Math.Pow(pheromone, _parameters.Alpha) * Math.Pow(visibility, _parameters.Beta);
                probabilities[station] = value;
                sumOfProbabilities += value;

                // Debug output for high queue time stations
                if (station.QueueTimeMin > 100)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"SelectNextStation: {station.Name} - QueueTime={station.QueueTimeMin:F1}min, " +
                        $"TravelTime={travelTime:F1}min, ChargeTime={chargeTime:F1}min, " +
                        $"TotalCost={totalCost:F1}min, Visibility={visibility:F8}, Value={value:F8}");
                }
            }

            // If no reachable candidate was added to probabilities, return null so caller can handle it safely.
            if (!probabilities.Any())
                return null;

            // NextDouble returns random between 0.0 and 1.0
            double pick;
            lock (_randomLock)
            {
                pick = _random.NextDouble() * sumOfProbabilities;
            }

            double cumulativeValue = 0;

            // kvp is Dictionary<Station, double>, with double being probability for station
            foreach (var kvp in probabilities)
            {
                cumulativeValue += kvp.Value;
                if (cumulativeValue >= pick)
                    return kvp.Key;
            }

            // As a final safe fallback, return one of the stations that contributed to probabilities.
            return probabilities.Keys.First();
        }

        private void UpdatePheromones(List<Ant> ants)
        {
            lock (_pheromoneUpdateLock)
            {
                // Evaporation step, (string, string) key being (fromStation.Id, toStation.Id)
                // _pheromones being Dictionary<(string, string), double>
                foreach (var key in _pheromones.Keys.ToList())
                {
                    _pheromones[key] *= (1 - _parameters.EvaporationRate);
                }

                // Deposition step, updates pheromones
                // NOTE: Delta is calculated based on TotalTime instead of TotalDistance
                // Rationale: EV Problem - time is more important than distance
                // Short route may have long charging times
                // Pheromones will reinforce routes with shorter total time
                foreach (var ant in ants)
                {
                    // guard against division by zero if TotalTime is zero
                    if (ant.TotalTime <= 0) continue;

                    for (int i = 0; i < ant.Path.Count - 1; i++)
                    {
                        var from = ant.Path[i];
                        // i+1 because it is next station in path
                        var to = ant.Path[i + 1];

                        var delta = 1.0 / ant.TotalTime; // instead TotalDistance
                        if (_pheromones.ContainsKey((from.Id, to.Id)))
                            _pheromones[(from.Id, to.Id)] += delta;
                    }
                }
            }
        }

        /// <summary>
        /// Decide whether charging should be allowed upon arrival to <paramref name="station"/>.
        /// Returns false (do not charge) when there exists at least one unvisited station (or destination)
        /// reachable from <paramref name="station"/> with the given <paramref name="energyOnArrivalKwh"/>.
        /// For destination stations this returns false.
        /// Adds a small random exploration chance so ants sometimes charge even when it's not strictly necessary.
        /// 
        /// IMPORTANT: This method only decides IF we CAN charge at a station, but does NOT prevent
        /// choosing a station with very high queue times. The actual penalty for queue time comes from
        /// the visibility calculation in SelectNextStation() which includes station.QueueTimeMin in totalCost.
        /// 
        /// If the visibility calculation is not preventing selection of high-queue stations, the issue
        /// is likely that Beta is too low or the totalCost calculation doesn't properly penalize queue times.
        /// </summary>
        private bool ShouldAllowChargingAtArrival(Ant ant, Station station, double energyOnArrivalKwh)
        {
            // If this is the destination, we don't charge there.
            if (_parameters.DestinationStation != null && station.Equals(_parameters.DestinationStation))
                return false;

            // If station cannot offer charging at all (respect OnlyPricedChargers), charging makes no sense.
            bool hasPotentialCharger = station.ChargerTypes != null && !string.IsNullOrEmpty(_parameters.ChargerType);
            if (_parameters.OnlyPricedChargers && !station.IsPricedCharger)
                hasPotentialCharger = false;

            if (!hasPotentialCharger)
                return false;

            // If from this station we can reach any remaining unvisited station (or destination) without charging,
            // prefer not to charge — but occasionally allow charging for exploration to avoid dead-ends.
            foreach (var candidate in _graph.Stations)
            {
                if (candidate.Id == station.Id) continue;
                if (ant.HasVisited(candidate.Id)) continue;

                var dist = _graph.GetDistance(station.Id, candidate.Id);
                if (double.IsInfinity(dist) || dist == double.MaxValue) continue;

                var energyNeeded = dist * _parameters.ConsumptionPerKm;
                if (energyNeeded <= energyOnArrivalKwh)
                {
                    // Usually do not charge, but with a small probability allow charging here (exploration).
                    double randomValue;
                    lock (_randomLock)
                    {
                        randomValue = _random.NextDouble();
                    }

                    if (randomValue < _chargeExplorationProbability)
                        return true; // allow charging despite reachability
                    return false; // reachable without charging -> do not charge at this station
                }
            }

            // No reachable next station without charging -> allow charging.
            return true;
        }

        /// <summary>
        /// Ensure the station instance recorded in ant.Path is the graph instance and that the graph instance
        /// has updated diagnostic fields copied from whatever station object the ant may have used internally.
        /// This fixes cases where Visit updated a different Station instance than the one the UI is bound to.
        /// </summary>
        private void SyncLastVisitedStationInstance(Ant ant)
        {
            if (ant == null) return;
            if (ant.Path == null || !ant.Path.Any()) return;

            var last = ant.Path.Last();
            if (last == null) return;

            var graphStation = _graph.Stations.FirstOrDefault(s => s.Id == last.Id);
            if (graphStation == null) return;

            // Copy diagnostic fields from the ant's station instance into the graph instance.
            graphStation.EnergyOnArrivalKwh = last.EnergyOnArrivalKwh;
            graphStation.EnergyOnDepartureKwh = last.EnergyOnDepartureKwh;
            graphStation.ChargingTimeMin = last.ChargingTimeMin;
            graphStation.WasChargedHere = last.WasChargedHere;

            // Replace the reference inside ant.Path so future updates target the graph instance.
            ant.Path[ant.Path.Count - 1] = graphStation;
        }
    }
}
