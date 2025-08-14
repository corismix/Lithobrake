using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lithobrake.Core
{
    /// <summary>
    /// Singleton physics manager for Lithobrake rocket simulation.
    /// Manages Jolt physics engine at fixed 60Hz timestep with vessel registration and performance monitoring.
    /// </summary>
    public partial class PhysicsManager : Node3D
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
            
            GD.Print($"PhysicsManager: Initialized with {FixedDelta:F6}s fixed delta (60Hz)");
            GD.Print("PhysicsManager: Jolt Physics engine configured");
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