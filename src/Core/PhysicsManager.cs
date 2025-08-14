using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lithobrake.Core
{
    /// <summary>
    /// Singleton physics manager for Lithobrake rocket simulation.
    /// Manages Jolt physics engine at fixed 60Hz timestep with vessel registration and performance monitoring.
    /// Coordinates with floating origin system for precision preservation.
    /// </summary>
    public partial class PhysicsManager : Node3D, IOriginShiftAware
    {
        private static PhysicsManager? _instance;
        public static PhysicsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PhysicsManager();
                }
                return _instance;
            }
        }

        // Physics constants
        public const double FixedDelta = 1.0 / 60.0; // 60Hz physics tick
        private const double PhysicsBudget = 5.0; // ms per frame budget

        // Physics server access (singleton)
        private RigidBody3D? _testBody; // For initial testing

        // Vessel management
        private readonly Dictionary<int, PhysicsVessel> _registeredVessels = new();
        private readonly List<PhysicsVessel> _activeVessels = new();
        private int _nextVesselId = 1;

        // Performance monitoring
        private readonly Stopwatch _physicsTimer = new();
        private readonly Queue<double> _physicsTimeSamples = new(60);
        private double _averagePhysicsTime = 0.0;
        private int _physicsTickCount = 0;

        // Collision layers (matching Godot's layer system)
        public const uint LayerStatic = 1;      // Static geometry
        public const uint LayerDynamic = 2;     // Dynamic objects  
        public const uint LayerVessel = 4;      // Vessel parts
        public const uint LayerDebris = 8;      // Separated parts
        
        // Floating origin system integration
        private bool _isOriginShiftRegistered = false;
        private Double3 _worldOriginOffset = Double3.Zero;

        public override void _Ready()
        {
            _instance = this;
            
            // Configure physics world settings for Jolt
            ConfigurePhysicsWorld();
            
            // Register with performance monitor
            if (PerformanceMonitor.IsInstanceValid)
            {
                GD.Print("PhysicsManager: Registered with PerformanceMonitor");
            }
            
            // Register with floating origin system (critical priority)
            FloatingOriginManager.RegisterOriginShiftAware(this);
            
            GD.Print($"PhysicsManager: Initialized with {FixedDelta:F6}s fixed delta (60Hz)");
            GD.Print("PhysicsManager: Jolt Physics engine configured with floating origin support");
        }

        private void ConfigurePhysicsWorld()
        {
            // Get the default physics world
            var world = GetWorld3D().Space;
            
            // Configure Jolt-specific settings through PhysicsServer3D
            PhysicsServer3D.Singleton.AreaSetParam(world, PhysicsServer3D.AreaParameter.Gravity, 9.81f);
            // Note: LinearDamping and AngularDamping are set via RigidBody properties in Godot 4
            
            GD.Print("PhysicsManager: Physics world configured for orbital mechanics");
        }

        public override void _PhysicsProcess(double delta)
        {
            _physicsTimer.Restart();
            
            // Process all registered vessels
            ProcessVessels();
            
            // Update physics timing
            _physicsTickCount++;
            
            _physicsTimer.Stop();
            double physicsTime = _physicsTimer.Elapsed.TotalMilliseconds;
            
            // Track performance
            RecordPhysicsTime(physicsTime);
            
            // Check performance budget
            if (physicsTime > PhysicsBudget)
            {
                GD.PrintErr($"⚠️ Physics budget exceeded: {physicsTime:F2}ms > {PhysicsBudget}ms");
            }
        }

        private void ProcessVessels()
        {
            // Process active vessels physics
            for (int i = _activeVessels.Count - 1; i >= 0; i--)
            {
                var vessel = _activeVessels[i];
                if (vessel == null || !GodotObject.IsInstanceValid(vessel))
                {
                    _activeVessels.RemoveAt(i);
                    continue;
                }
                
                vessel.ProcessPhysics(FixedDelta);
                
                // Monitor vessel position for floating origin shifts
                var vesselState = vessel.GetVesselState();
                if (vesselState.IsActive)
                {
                    FloatingOriginManager.MonitorOriginDistance(vesselState.Position);
                }
            }
        }

        private void RecordPhysicsTime(double time)
        {
            _physicsTimeSamples.Enqueue(time);
            if (_physicsTimeSamples.Count > 60)
                _physicsTimeSamples.Dequeue();
                
            // Calculate running average
            double sum = 0;
            foreach (double sample in _physicsTimeSamples)
                sum += sample;
            _averagePhysicsTime = sum / _physicsTimeSamples.Count;
            
            // Update performance monitor if available
            if (PerformanceMonitor.IsInstanceValid)
            {
                // Performance monitor will track this automatically through _PhysicsProcess
            }
        }

        /// <summary>
        /// Register a new vessel with the physics system
        /// </summary>
        public int RegisterVessel(PhysicsVessel vessel)
        {
            int vesselId = _nextVesselId++;
            _registeredVessels[vesselId] = vessel;
            _activeVessels.Add(vessel);
            
            vessel.Initialize(vesselId, this);
            
            GD.Print($"PhysicsManager: Registered vessel {vesselId} with {vessel.GetPartCount()} parts");
            return vesselId;
        }

        /// <summary>
        /// Unregister vessel from physics system
        /// </summary>
        public void UnregisterVessel(int vesselId)
        {
            if (_registeredVessels.TryGetValue(vesselId, out var vessel))
            {
                _registeredVessels.Remove(vesselId);
                _activeVessels.Remove(vessel);
                
                vessel.Cleanup();
                GD.Print($"PhysicsManager: Unregistered vessel {vesselId}");
            }
        }

        /// <summary>
        /// Get vessel by ID
        /// </summary>
        public PhysicsVessel? GetVessel(int vesselId)
        {
            _registeredVessels.TryGetValue(vesselId, out var vessel);
            return vessel;
        }

        /// <summary>
        /// Create a test rigid body for physics validation
        /// </summary>
        public RigidBody3D CreateTestBody()
        {
            var body = new RigidBody3D();
            var shape = new SphereShape3D();
            shape.Radius = 0.5f;
            
            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = shape;
            
            body.AddChild(collisionShape);
            body.Mass = 1.0f;
            body.CollisionLayer = LayerDynamic;
            body.CollisionMask = LayerStatic | LayerDynamic;
            
            AddChild(body);
            _testBody = body;
            
            GD.Print("PhysicsManager: Created test rigid body");
            return body;
        }

        /// <summary>
        /// Remove test body
        /// </summary>
        public void DestroyTestBody()
        {
            if (_testBody != null && GodotObject.IsInstanceValid(_testBody))
            {
                _testBody.QueueFree();
                _testBody = null!;
                GD.Print("PhysicsManager: Destroyed test rigid body");
            }
        }

        /// <summary>
        /// Get current physics performance metrics
        /// </summary>
        public PhysicsMetrics GetPhysicsMetrics()
        {
            return new PhysicsMetrics
            {
                AveragePhysicsTime = _averagePhysicsTime,
                PhysicsTickCount = _physicsTickCount,
                RegisteredVesselCount = _registeredVessels.Count,
                ActiveVesselCount = _activeVessels.Count,
                PhysicsBudget = PhysicsBudget,
                FixedDeltaTime = FixedDelta,
                IsPerformingWithinBudget = _averagePhysicsTime <= PhysicsBudget
            };
        }

        /// <summary>
        /// Validate physics system performance
        /// </summary>
        public bool ValidatePerformance()
        {
            var metrics = GetPhysicsMetrics();
            
            bool withinBudget = metrics.IsPerformingWithinBudget;
            bool consistentTiming = _physicsTimeSamples.Count >= 60; // Full second of samples
            
            GD.Print($"Physics Performance Validation:");
            GD.Print($"  Average Physics Time: {metrics.AveragePhysicsTime:F2}ms (budget: {PhysicsBudget}ms)");
            GD.Print($"  Within Budget: {withinBudget}");
            GD.Print($"  Samples Collected: {_physicsTimeSamples.Count}/60");
            GD.Print($"  Physics Ticks: {metrics.PhysicsTickCount}");
            
            return withinBudget && consistentTiming;
        }

        /// <summary>
        /// Initialize singleton for global access
        /// </summary>
        public static void InitializeSingleton()
        {
            if (_instance == null)
            {
                _instance = new PhysicsManager();
                GD.Print("PhysicsManager singleton created programmatically");
            }
        }

        /// <summary>
        /// Check if singleton is valid
        /// </summary>
        public static new bool IsInstanceValid => _instance != null && GodotObject.IsInstanceValid(_instance);
        
        /// <summary>
        /// Check if vessel should trigger coast period for origin shift
        /// </summary>
        public bool IsInCoastPeriod()
        {
            // Check all active vessels for coast period conditions
            foreach (var vessel in _activeVessels)
            {
                if (vessel == null || !GodotObject.IsInstanceValid(vessel))
                    continue;
                
                var (altitude, velocity, _, inAtmosphere) = vessel.GetOrbitalMetrics();
                
                if (inAtmosphere)
                {
                    // Check dynamic pressure using atmospheric conditions
                    var dynamicPressure = AtmosphericConditions.GetDynamicPressure(altitude, velocity);
                    
                    if (dynamicPressure > 1000.0) // 1 kPa max dynamic pressure for shifts
                    {
                        return false;
                    }
                }
                
                // TODO: Add thrust checking when propulsion system is implemented
                // For now, assume coast period if dynamic pressure is low
            }
            
            return true; // Safe to perform origin shift
        }
        
        #region IOriginShiftAware Implementation
        
        /// <summary>
        /// Handle floating origin shift by coordinating physics system updates
        /// </summary>
        /// <param name="deltaPosition">The coordinate shift amount</param>
        public void HandleOriginShift(Double3 deltaPosition)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Update world origin offset for reference
                _worldOriginOffset += deltaPosition;
                
                // Update test body position if it exists
                if (_testBody != null && GodotObject.IsInstanceValid(_testBody))
                {
                    var currentPos = Double3.FromVector3(_testBody.GlobalPosition);
                    var newPos = currentPos + deltaPosition;
                    _testBody.GlobalPosition = newPos.ToVector3();
                }
                
                // Note: Vessels handle their own origin shifts through their IOriginShiftAware implementation
                // PhysicsManager just needs to maintain physics world coherence
                
                stopwatch.Stop();
                double shiftTime = stopwatch.Elapsed.TotalMilliseconds;
                
                GD.Print($"PhysicsManager: Handled origin shift in {shiftTime:F3}ms, world offset now {_worldOriginOffset.Length:F1}m");
                
                if (shiftTime > 0.05) // 0.05ms target for physics manager
                {
                    GD.PrintErr($"PhysicsManager: Origin shift handling took {shiftTime:F3}ms (target: 0.05ms)");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"PhysicsManager: Error handling origin shift: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Whether this physics manager is registered for origin shift notifications
        /// </summary>
        public bool IsRegistered
        {
            get => _isOriginShiftRegistered;
            set => _isOriginShiftRegistered = value;
        }
        
        /// <summary>
        /// Critical priority for physics system (updated first)
        /// </summary>
        public int ShiftPriority => OriginShiftPriority.Critical;
        
        /// <summary>
        /// Always receive origin shift notifications when registered
        /// </summary>
        public bool ShouldReceiveOriginShifts => IsRegistered;
        
        /// <summary>
        /// Get current world origin offset for debugging
        /// </summary>
        public Double3 GetWorldOriginOffset() => _worldOriginOffset;
        
        #endregion
    }

    /// <summary>
    /// Physics system metrics structure
    /// </summary>
    public struct PhysicsMetrics
    {
        public double AveragePhysicsTime;
        public int PhysicsTickCount;
        public int RegisteredVesselCount;
        public int ActiveVesselCount;
        public double PhysicsBudget;
        public double FixedDeltaTime;
        public bool IsPerformingWithinBudget;
    }
}