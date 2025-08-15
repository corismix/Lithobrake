using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Fuel flow system managing fuel routing, consumption, and crossfeed between tanks.
    /// Implements tree traversal for fuel flow from engines to tanks (bottom to top).
    /// Handles fuel starvation, priority systems, and crossfeed blocking at decouplers.
    /// </summary>
    public static class FuelFlowSystem
    {
        // Performance tracking
        private static double _lastUpdateTime = 0.0;
        private static int _tanksProcessed = 0;
        
        // Performance targets from current-task.md
        private const double FuelConsumptionBudget = 0.1; // ms per frame per fuel tank
        
        // Fuel flow constants
        private const double FuelTransferRate = 100.0; // Units per second default
        private const double MinimumFuelTransfer = 0.1; // Minimum fuel to transfer
        private const double FuelStarvationThreshold = 0.1; // Fuel level for starvation
        
        /// <summary>
        /// Update fuel flow for all engines and tanks in a vessel
        /// </summary>
        /// <param name="engines">Engines consuming fuel</param>
        /// <param name="fuelTanks">Fuel tanks providing fuel</param>
        /// <param name="deltaTime">Time step in seconds</param>
        public static FuelFlowResult UpdateFuelFlow(IEnumerable<Engine> engines, IEnumerable<FuelTank> fuelTanks, double deltaTime)
        {
            var startTime = Time.GetTicksMsec();
            var result = new FuelFlowResult();
            
            if (engines == null || fuelTanks == null || deltaTime <= 0)
                return result;
            
            var activeEngines = engines.Where(e => e != null && !e.IsQueuedForDeletion() && e.IsActive).ToList();
            var availableTanks = fuelTanks.Where(t => t != null && !t.IsQueuedForDeletion()).ToList();
            
            _tanksProcessed = availableTanks.Count;
            
            // Process fuel consumption for each engine
            foreach (var engine in activeEngines)
            {
                var engineResult = ProcessEngineConsumption(engine, availableTanks, deltaTime);
                result.TotalFuelConsumed += engineResult.FuelConsumed;
                result.EngineResults.Add(engineResult);
                
                // Handle fuel starvation
                if (!engineResult.HasAdequateFuel && engine.IsActive)
                {
                    result.StarvationEvents.Add(new FuelStarvationEvent(engine)
                    {
                        RequestedFuel = engineResult.FuelRequested,
                        AvailableFuel = engineResult.FuelConsumed,
                        StarvationTime = Time.GetUnixTimeFromSystem()
                    });
                }
            }
            
            // Process crossfeed between tanks
            ProcessCrossfeed(availableTanks, deltaTime);
            
            // Update tank mass properties
            UpdateTankMasses(availableTanks);
            
            // Performance monitoring
            var duration = Time.GetTicksMsec() - startTime;
            _lastUpdateTime = duration;
            
            if (duration > FuelConsumptionBudget * availableTanks.Count)
            {
                GD.PrintErr($"FuelFlowSystem: Update exceeded budget - {duration:F2}ms for {availableTanks.Count} tanks (target: {FuelConsumptionBudget * availableTanks.Count:F2}ms)");
            }
            
            return result;
        }
        
        /// <summary>
        /// Process fuel consumption for a single engine
        /// Implements tree traversal from engine to connected tanks
        /// </summary>
        /// <param name="engine">Engine consuming fuel</param>
        /// <param name="availableTanks">Available fuel tanks</param>
        /// <param name="deltaTime">Time step in seconds</param>
        /// <returns>Engine fuel consumption result</returns>
        private static EngineFuelResult ProcessEngineConsumption(Engine engine, List<FuelTank> availableTanks, double deltaTime)
        {
            var result = new EngineFuelResult(engine)
            {
                FuelRequested = engine.FuelConsumption * (engine.CurrentThrust / engine.MaxThrust) * deltaTime
            };
            
            if (result.FuelRequested <= 0)
            {
                result.HasAdequateFuel = true;
                return result;
            }
            
            // Build fuel network tree from engine position (bottom to top)
            var fuelNetwork = BuildFuelNetworkTree(engine, availableTanks);
            
            // Drain fuel following priority order
            var remainingNeeded = result.FuelRequested;
            
            foreach (var tank in fuelNetwork.OrderByDescending(t => GetTankPriority(t, engine)))
            {
                if (remainingNeeded <= 0)
                    break;
                
                var fuelDrained = tank.DrainFuel(remainingNeeded, FuelType.LiquidFuel);
                result.FuelConsumed += fuelDrained;
                remainingNeeded -= fuelDrained;
                
                if (fuelDrained > 0)
                {
                    result.TanksUsed.Add(tank);
                }
            }
            
            // Check if we got adequate fuel (90% efficiency threshold)
            result.HasAdequateFuel = result.FuelConsumed >= (result.FuelRequested * 0.9);
            
            return result;
        }
        
        /// <summary>
        /// Build fuel network tree traversal from engine to accessible tanks
        /// Implements bottom-to-top fuel flow through vessel structure
        /// </summary>
        /// <param name="engine">Starting engine</param>
        /// <param name="availableTanks">All available tanks</param>
        /// <returns>Ordered list of accessible tanks</returns>
        private static List<FuelTank> BuildFuelNetworkTree(Engine engine, List<FuelTank> availableTanks)
        {
            var accessibleTanks = new List<FuelTank>();
            var visitedTanks = new HashSet<FuelTank>();
            
            // For now, simplified approach: tanks connected to engine through crossfeed
            // In full implementation, this would traverse vessel part tree
            foreach (var tank in availableTanks)
            {
                if (IsAccessibleFromEngine(engine, tank))
                {
                    accessibleTanks.Add(tank);
                }
            }
            
            return accessibleTanks;
        }
        
        /// <summary>
        /// Check if fuel tank is accessible from engine (crossfeed enabled path)
        /// </summary>
        /// <param name="engine">Source engine</param>
        /// <param name="tank">Target fuel tank</param>
        /// <returns>True if tank can supply fuel to engine</returns>
        private static bool IsAccessibleFromEngine(Engine engine, FuelTank tank)
        {
            // Simplified accessibility check
            // Real implementation would traverse vessel structure checking crossfeed blocks
            
            if (!tank.CanCrossfeed)
                return false;
            
            // Check if tank has sufficient fuel
            if (tank.IsEmpty())
                return false;
            
            // For now, assume all crossfeed-enabled tanks are accessible
            // In full implementation, check for decouplers and structural separations
            return true;
        }
        
        /// <summary>
        /// Get fuel priority for tank relative to engine
        /// Higher priority tanks are drained first
        /// </summary>
        /// <param name="tank">Fuel tank to evaluate</param>
        /// <param name="engine">Consuming engine</param>
        /// <returns>Priority value (higher = higher priority)</returns>
        private static double GetTankPriority(FuelTank tank, Engine engine)
        {
            var priority = 0.0;
            
            // Distance factor: closer tanks have higher priority
            var distance = engine.GlobalPosition.DistanceTo(tank.GlobalPosition);
            priority += (100.0 - Math.Min(distance, 100.0)); // Max 100 priority from distance
            
            // Fuel level factor: fuller tanks have slightly higher priority
            priority += tank.GetFuelPercentage() * 10.0; // Max 10 priority from fuel level
            
            // Tank type factor: liquid fuel tanks have higher priority for engines
            if (tank.TankType == FuelTankType.LiquidFuel || tank.TankType == FuelTankType.LiquidOxidizer)
            {
                priority += 20.0; // Bonus for compatible tanks
            }
            
            return priority;
        }
        
        /// <summary>
        /// Process crossfeed between connected fuel tanks
        /// </summary>
        /// <param name="fuelTanks">Fuel tanks to process crossfeed for</param>
        /// <param name="deltaTime">Time step in seconds</param>
        private static void ProcessCrossfeed(List<FuelTank> fuelTanks, double deltaTime)
        {
            foreach (var tank in fuelTanks.Where(t => t.CanCrossfeed && t.IsTransferring))
            {
                foreach (var connectedTank in tank.ConnectedTanks.ToList())
                {
                    if (connectedTank == null || connectedTank.IsQueuedForDeletion())
                    {
                        tank.ConnectedTanks.Remove(connectedTank);
                        continue;
                    }
                    
                    TransferFuelBetweenTanks(tank, connectedTank, deltaTime);
                }
            }
        }
        
        /// <summary>
        /// Transfer fuel between two tanks based on fuel level equalization
        /// </summary>
        /// <param name="sourceTank">Source fuel tank</param>
        /// <param name="targetTank">Target fuel tank</param>
        /// <param name="deltaTime">Time step in seconds</param>
        private static void TransferFuelBetweenTanks(FuelTank sourceTank, FuelTank targetTank, double deltaTime)
        {
            if (!sourceTank.CanCrossfeed || !targetTank.CanCrossfeed)
                return;
            
            var sourceFuelPercentage = sourceTank.GetFuelPercentage();
            var targetFuelPercentage = targetTank.GetFuelPercentage();
            
            // Only transfer if source has significantly more fuel than target
            if (sourceFuelPercentage <= targetFuelPercentage + 0.05) // 5% threshold
                return;
            
            var transferRate = Math.Min(sourceTank.TransferRate, targetTank.TransferRate);
            var transferAmount = transferRate * deltaTime;
            
            // Calculate actual transferable amount
            var maxTransfer = Math.Min(transferAmount, sourceTank.LiquidFuel);
            maxTransfer = Math.Min(maxTransfer, targetTank.LiquidFuelMax - targetTank.LiquidFuel);
            
            if (maxTransfer >= MinimumFuelTransfer)
            {
                sourceTank.LiquidFuel -= maxTransfer;
                targetTank.LiquidFuel += maxTransfer;
                
                // Update masses
                sourceTank.UpdateFuelMass();
                targetTank.UpdateFuelMass();
            }
        }
        
        /// <summary>
        /// Update mass properties for all fuel tanks
        /// </summary>
        /// <param name="fuelTanks">Tanks to update</param>
        private static void UpdateTankMasses(List<FuelTank> fuelTanks)
        {
            foreach (var tank in fuelTanks)
            {
                tank.UpdateFuelMass();
            }
        }
        
        /// <summary>
        /// Check for fuel starvation in engines
        /// </summary>
        /// <param name="engines">Engines to check</param>
        /// <param name="fuelTanks">Available fuel tanks</param>
        /// <returns>List of engines experiencing fuel starvation</returns>
        public static List<Engine> CheckFuelStarvation(IEnumerable<Engine> engines, IEnumerable<FuelTank> fuelTanks)
        {
            var starvedEngines = new List<Engine>();
            
            foreach (var engine in engines.Where(e => e != null && !e.IsQueuedForDeletion() && e.IsActive))
            {
                var accessibleTanks = BuildFuelNetworkTree(engine, fuelTanks.ToList());
                var availableFuel = accessibleTanks.Sum(t => t.LiquidFuel);
                
                if (availableFuel < FuelStarvationThreshold)
                {
                    starvedEngines.Add(engine);
                }
            }
            
            return starvedEngines;
        }
        
        /// <summary>
        /// Get fuel flow system performance metrics
        /// </summary>
        /// <returns>Performance statistics</returns>
        public static FuelFlowSystemMetrics GetPerformanceMetrics()
        {
            return new FuelFlowSystemMetrics
            {
                LastUpdateTime = _lastUpdateTime,
                TanksProcessed = _tanksProcessed,
                ConsumptionBudget = FuelConsumptionBudget,
                IsWithinBudget = _lastUpdateTime <= (FuelConsumptionBudget * _tanksProcessed)
            };
        }
    }
    
    /// <summary>
    /// Result of fuel flow processing for entire vessel
    /// </summary>
    public class FuelFlowResult
    {
        public double TotalFuelConsumed { get; set; } = 0.0;
        public List<EngineFuelResult> EngineResults { get; set; } = new();
        public List<FuelStarvationEvent> StarvationEvents { get; set; } = new();
    }
    
    /// <summary>
    /// Fuel consumption result for individual engine
    /// </summary>
    public class EngineFuelResult
    {
        private Engine? _engine;
        public Engine Engine 
        { 
            get => _engine ?? throw new InvalidOperationException("Engine not initialized in EngineFuelResult");
            set => _engine = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public double FuelRequested { get; set; } = 0.0;
        public double FuelConsumed { get; set; } = 0.0;
        public bool HasAdequateFuel { get; set; } = false;
        public List<FuelTank> TanksUsed { get; set; } = new();
        
        /// <summary>
        /// Constructor requiring engine to be set
        /// </summary>
        public EngineFuelResult(Engine engine)
        {
            Engine = engine; // Uses the setter with validation
        }
        
        /// <summary>
        /// Checks if the engine is valid and can be safely used
        /// </summary>
        public bool IsEngineValid => SafeOperations.IsValid(_engine, "EngineFuelResult.Engine");
    }
    
    /// <summary>
    /// Fuel starvation event data
    /// </summary>
    public class FuelStarvationEvent
    {
        private Engine? _engine;
        public Engine Engine 
        { 
            get => _engine ?? throw new InvalidOperationException("Engine not initialized in FuelStarvationEvent");
            set => _engine = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public double RequestedFuel { get; set; }
        public double AvailableFuel { get; set; }
        public double StarvationTime { get; set; }
        
        /// <summary>
        /// Constructor requiring engine to be set
        /// </summary>
        public FuelStarvationEvent(Engine engine)
        {
            Engine = engine; // Uses the setter with validation
        }
        
        /// <summary>
        /// Checks if the engine is valid and can be safely used
        /// </summary>
        public bool IsEngineValid => SafeOperations.IsValid(_engine, "FuelStarvationEvent.Engine");
    }
    
    /// <summary>
    /// Fuel flow system performance metrics
    /// </summary>
    public struct FuelFlowSystemMetrics
    {
        public double LastUpdateTime;
        public int TanksProcessed;
        public double ConsumptionBudget;
        public bool IsWithinBudget;
    }
}