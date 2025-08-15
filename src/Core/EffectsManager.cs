using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Effects manager for engine exhaust particles, thrust visualization, and visual feedback.
    /// Manages object pooling, performance optimization, and atmospheric effects scaling.
    /// Provides thrust vector visualization and engine state visual indicators.
    /// </summary>
    public partial class EffectsManager : Node3D
    {
        // Singleton pattern for global access
        private static EffectsManager? _instance;
        public static EffectsManager Instance => _instance ?? throw new InvalidOperationException("EffectsManager not initialized");
        
        // Object pools for performance optimization
        private readonly ObjectPool<GpuParticles3D> _exhaustParticlePool = new();
        private readonly ObjectPool<MeshInstance3D> _thrustArrowPool = new();
        private readonly ObjectPool<SpotLight3D> _exhaustLightPool = new();
        
        // Active effects tracking
        private readonly Dictionary<Engine, ExhaustEffect> _engineEffects = new();
        private readonly Dictionary<Engine, ThrustVisualization> _thrustVisualizations = new();
        
        // Performance tracking
        private double _lastUpdateTime = 0.0;
        private int _activeEffects = 0;
        
        // Performance targets from current-task.md
        private const double EffectsUpdateBudget = 1.0; // ms per frame using object pooling
        private const double ThrustVisualizationBudget = 0.5; // ms per frame for all engines
        private const double HeatingEffectsIntegrationBudget = 0.1; // ms per frame for heating coordination
        
        // Visual settings
        private const float MaxExhaustScale = 2.0f;
        private const float MinExhaustScale = 0.1f;
        private const float ThrustArrowLength = 3.0f;
        private const float ExhaustLightIntensity = 2.0f;
        
        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                InitializeObjectPools();
                GD.Print("EffectsManager: Initialized as singleton with object pooling and heating effects integration");
            }
            else
            {
                GD.PrintErr("EffectsManager: Multiple instances detected!");
                QueueFree();
            }
        }
        
        /// <summary>
        /// Initialize object pools for performance optimization
        /// </summary>
        private void InitializeObjectPools()
        {
            // Pre-populate pools with commonly needed objects
            for (int i = 0; i < 10; i++)
            {
                _exhaustParticlePool.Return(CreateExhaustParticleSystem());
                _thrustArrowPool.Return(CreateThrustArrow());
                _exhaustLightPool.Return(CreateExhaustLight());
            }
        }
        
        /// <summary>
        /// Update all engine effects
        /// </summary>
        public override void _Process(double delta)
        {
            var startTime = Time.GetTicksMsec();
            
            UpdateEngineEffects(delta);
            UpdateThrustVisualizations(delta);
            CleanupInactiveEffects();
            
            // Performance monitoring
            var duration = Time.GetTicksMsec() - startTime;
            _lastUpdateTime = duration;
            
            if (duration > EffectsUpdateBudget)
            {
                GD.PrintErr($"EffectsManager: Update exceeded budget - {duration:F2}ms (target: {EffectsUpdateBudget:F2}ms)");
            }
        }
        
        /// <summary>
        /// Create or update exhaust effect for engine
        /// </summary>
        /// <param name="engine">Engine to create effect for</param>
        /// <param name="throttle">Engine throttle (0-1)</param>
        /// <param name="atmosphericPressure">Atmospheric pressure in Pa</param>
        public void UpdateEngineExhaust(Engine engine, double throttle, double atmosphericPressure)
        {
            if (engine == null || engine.IsQueuedForDeletion())
                return;
            
            if (!_engineEffects.TryGetValue(engine, out var effect))
            {
                effect = CreateEngineExhaust(engine);
                _engineEffects[engine] = effect;
            }
            
            UpdateExhaustEffect(effect, engine, throttle, atmosphericPressure);
        }
        
        /// <summary>
        /// Create or update thrust visualization for engine
        /// </summary>
        /// <param name="engine">Engine to visualize thrust for</param>
        /// <param name="thrust">Current thrust in Newtons</param>
        /// <param name="efficiency">Engine efficiency (0-1)</param>
        public void UpdateThrustVisualization(Engine engine, double thrust, double efficiency)
        {
            if (engine == null || engine.IsQueuedForDeletion())
                return;
            
            if (!_thrustVisualizations.TryGetValue(engine, out var visualization))
            {
                visualization = CreateThrustVisualization(engine);
                _thrustVisualizations[engine] = visualization;
            }
            
            UpdateThrustArrow(visualization, engine, thrust, efficiency);
        }
        
        /// <summary>
        /// Update all visual effects for a vessel including exhaust, thrust, and heating
        /// Coordinates with HeatingEffects and DynamicPressure systems for comprehensive visualization
        /// </summary>
        /// <param name="vessel">Vessel to update effects for</param>
        public static void UpdateVesselEffects(PhysicsVessel vessel)
        {
            if (vessel?.Parts == null || Instance == null)
                return;
            
            var startTime = Time.GetTicksMsec();
            
            // Get atmospheric properties for heating and exhaust scaling
            var atmosphericProperties = Atmosphere.GetVesselAtmosphericProperties(vessel);
            
            // Update engine-specific effects (exhaust and thrust visualization)
            foreach (var part in vessel.Parts)
            {
                if (part is Engine engine && engine.IsActive)
                {
                    // Update exhaust effects with atmospheric scaling
                    Instance.UpdateEngineExhaust(engine, engine.CurrentThrottle, atmosphericProperties.Pressure);
                    
                    // Update thrust visualization
                    var thrust = engine.GetThrust(engine.CurrentThrottle, atmosphericProperties.Pressure);
                    var efficiency = CalculateEngineEfficiency(engine, atmosphericProperties);
                    Instance.UpdateThrustVisualization(engine, thrust, efficiency);
                }
            }
            
            // Coordinate with heating effects system
            var vesselVelocity = vessel.RootPart?.RigidBody?.LinearVelocity ?? Vector3.Zero;
            var dynamicPressure = DynamicPressure.CalculateQ(vesselVelocity, atmosphericProperties.Density);
            var heatingIntensity = dynamicPressure * vesselVelocity.Length();
            HeatingEffects.UpdateVesselHeating(vessel, heatingIntensity);
            
            // Performance monitoring
            var duration = Time.GetTicksMsec() - startTime;
            if (duration > HeatingEffectsIntegrationBudget)
            {
                GD.PrintErr($"EffectsManager: Vessel effects update exceeded budget - {duration:F2}ms (target: {HeatingEffectsIntegrationBudget:F2}ms)");
            }
        }
        
        /// <summary>
        /// Calculate engine efficiency based on atmospheric conditions
        /// </summary>
        /// <param name="engine">Engine to calculate efficiency for</param>
        /// <param name="atmosphericProperties">Atmospheric conditions</param>
        /// <returns>Engine efficiency (0-1)</returns>
        private static double CalculateEngineEfficiency(Engine engine, AtmosphericProperties atmosphericProperties)
        {
            // Simplified efficiency model - engines are more efficient in vacuum
            var seaLevelPressure = 101325.0; // Pa
            var pressureRatio = Math.Clamp(atmosphericProperties.Pressure / seaLevelPressure, 0.0, 1.0);
            
            // Vacuum engines: efficiency decreases with atmospheric pressure
            // Atmospheric engines: efficiency optimized for sea level
            // For now, assume all engines are vacuum-optimized (like rocket engines)
            var vacuumEfficiency = 1.0;
            var atmosphericEfficiency = 0.7; // Reduced efficiency at sea level
            
            return atmosphericEfficiency + (vacuumEfficiency - atmosphericEfficiency) * (1.0 - pressureRatio);
        }
        
        /// <summary>
        /// Create exhaust effect for engine
        /// </summary>
        /// <param name="engine">Engine to create effect for</param>
        /// <returns>Exhaust effect data</returns>
        private ExhaustEffect CreateEngineExhaust(Engine engine)
        {
            var particles = _exhaustParticlePool.Get();
            var light = _exhaustLightPool.Get();
            
            // Position exhaust at engine nozzle
            var exhaustPosition = engine.GlobalPosition - engine.Transform.Basis.Y * 0.8f;
            
            particles.GlobalPosition = exhaustPosition;
            particles.ProcessMaterial = CreateExhaustMaterial();
            light.GlobalPosition = exhaustPosition;
            light.LightEnergy = 0.0f;
            
            // Add to scene
            AddChild(particles);
            AddChild(light);
            
            return new ExhaustEffect(particles, light, engine)
            {
                IsActive = false
            };
        }
        
        /// <summary>
        /// Create thrust visualization for engine
        /// </summary>
        /// <param name="engine">Engine to create visualization for</param>
        /// <returns>Thrust visualization data</returns>
        private ThrustVisualization CreateThrustVisualization(Engine engine)
        {
            var arrow = _thrustArrowPool.Get();
            
            // Position arrow at engine mount point
            arrow.GlobalPosition = engine.GlobalPosition;
            arrow.Scale = Vector3.One * 0.1f; // Start hidden
            
            AddChild(arrow);
            
            return new ThrustVisualization(arrow, engine)
            {
                IsVisible = false
            };
        }
        
        /// <summary>
        /// Update exhaust effect based on engine state
        /// </summary>
        /// <param name="effect">Exhaust effect to update</param>
        /// <param name="engine">Engine providing data</param>
        /// <param name="throttle">Engine throttle</param>
        /// <param name="atmosphericPressure">Atmospheric pressure</param>
        private void UpdateExhaustEffect(ExhaustEffect effect, Engine engine, double throttle, double atmosphericPressure)
        {
            var shouldBeActive = engine.IsActive && throttle > 0.01;
            
            if (shouldBeActive != effect.IsActive)
            {
                effect.IsActive = shouldBeActive;
                effect.ParticleSystem.Emitting = shouldBeActive;
            }
            
            if (shouldBeActive)
            {
                // Scale exhaust based on throttle and atmospheric conditions
                var exhaustScale = CalculateExhaustScale(throttle, atmosphericPressure);
                var exhaustIntensity = (float)(throttle * ExhaustLightIntensity);
                
                // Update particle system
                var material = effect.ParticleSystem.ProcessMaterial as ParticleProcessMaterial;
                if (material != null)
                {
                    material.Scale = new Vector2(exhaustScale, exhaustScale);
                    material.InitialVelocity = new Vector2((float)(throttle * 20.0), 0); // Base velocity 20 m/s
                }
                
                // Update exhaust light
                effect.ExhaustLight.LightEnergy = exhaustIntensity;
                effect.ExhaustLight.LightColor = GetExhaustColor(atmosphericPressure);
                
                // Update position to follow engine
                var exhaustPosition = engine.GlobalPosition - engine.Transform.Basis.Y * 0.8f;
                effect.ParticleSystem.GlobalPosition = exhaustPosition;
                effect.ExhaustLight.GlobalPosition = exhaustPosition;
            }
            else
            {
                effect.ExhaustLight.LightEnergy = 0.0f;
            }
        }
        
        /// <summary>
        /// Update thrust visualization arrow
        /// </summary>
        /// <param name="visualization">Thrust visualization to update</param>
        /// <param name="engine">Engine providing data</param>
        /// <param name="thrust">Current thrust</param>
        /// <param name="efficiency">Engine efficiency</param>
        private void UpdateThrustArrow(ThrustVisualization visualization, Engine engine, double thrust, double efficiency)
        {
            var shouldBeVisible = thrust > 100.0; // Show for thrust > 100N
            
            if (shouldBeVisible != visualization.IsVisible)
            {
                visualization.IsVisible = shouldBeVisible;
                visualization.ThrustArrow.Visible = shouldBeVisible;
            }
            
            if (shouldBeVisible)
            {
                // Scale arrow by thrust magnitude
                var thrustScale = Math.Clamp(thrust / engine.MaxThrust, 0.1, 2.0);
                var arrowLength = ThrustArrowLength * thrustScale;
                
                // Color-code by efficiency (green = high efficiency, red = low efficiency)
                var arrowColor = GetEfficiencyColor(efficiency);
                
                // Update arrow properties
                visualization.ThrustArrow.Scale = new Vector3(0.2f, (float)arrowLength, 0.2f);
                visualization.ThrustArrow.GlobalPosition = engine.GlobalPosition;
                visualization.ThrustArrow.GlobalRotation = engine.GlobalRotation;
                
                // Apply color to arrow material
                var material = visualization.ThrustArrow.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
                if (material != null)
                {
                    material.AlbedoColor = arrowColor;
                }
            }
        }
        
        /// <summary>
        /// Calculate exhaust scale based on throttle and atmospheric conditions
        /// </summary>
        /// <param name="throttle">Engine throttle</param>
        /// <param name="atmosphericPressure">Atmospheric pressure</param>
        /// <returns>Exhaust scale factor</returns>
        private float CalculateExhaustScale(double throttle, double atmosphericPressure)
        {
            var baseScale = (float)Math.Clamp(throttle, MinExhaustScale, MaxExhaustScale);
            
            // Exhaust expands more in vacuum
            var seaLevelPressure = 101325.0; // Pa
            var pressureRatio = Math.Clamp(atmosphericPressure / seaLevelPressure, 0.0, 1.0);
            var vacuumExpansion = 1.0f + (1.0f - (float)pressureRatio) * 0.5f; // Up to 50% larger in vacuum
            
            return baseScale * vacuumExpansion;
        }
        
        /// <summary>
        /// Get exhaust color based on atmospheric conditions
        /// </summary>
        /// <param name="atmosphericPressure">Atmospheric pressure</param>
        /// <returns>Exhaust flame color</returns>
        private Color GetExhaustColor(double atmosphericPressure)
        {
            var seaLevelPressure = 101325.0; // Pa
            var pressureRatio = Math.Clamp(atmosphericPressure / seaLevelPressure, 0.0, 1.0);
            
            // Blue-white in vacuum, orange-yellow at sea level
            var vacuumColor = new Color(0.8f, 0.9f, 1.0f); // Blue-white
            var atmosphericColor = new Color(1.0f, 0.6f, 0.2f); // Orange-yellow
            
            return atmosphericColor.Lerp(vacuumColor, 1.0f - (float)pressureRatio);
        }
        
        /// <summary>
        /// Get efficiency color for thrust visualization
        /// </summary>
        /// <param name="efficiency">Engine efficiency (0-1)</param>
        /// <returns>Color representing efficiency</returns>
        private Color GetEfficiencyColor(double efficiency)
        {
            var clampedEfficiency = Math.Clamp(efficiency, 0.0, 1.0);
            
            // Red for low efficiency, green for high efficiency
            var lowEfficiencyColor = new Color(1.0f, 0.2f, 0.2f); // Red
            var highEfficiencyColor = new Color(0.2f, 1.0f, 0.2f); // Green
            
            return lowEfficiencyColor.Lerp(highEfficiencyColor, (float)clampedEfficiency);
        }
        
        /// <summary>
        /// Update all engine effects
        /// </summary>
        /// <param name="delta">Time step</param>
        private void UpdateEngineEffects(double delta)
        {
            _activeEffects = 0;
            
            foreach (var kvp in _engineEffects.ToList())
            {
                var engine = kvp.Key;
                var effect = kvp.Value;
                
                if (engine == null || engine.IsQueuedForDeletion())
                {
                    RemoveEngineEffect(engine);
                    continue;
                }
                
                if (effect.IsActive)
                    _activeEffects++;
            }
        }
        
        /// <summary>
        /// Update all thrust visualizations
        /// </summary>
        /// <param name="delta">Time step</param>
        private void UpdateThrustVisualizations(double delta)
        {
            var startTime = Time.GetTicksMsec();
            
            foreach (var kvp in _thrustVisualizations.ToList())
            {
                var engine = kvp.Key;
                var visualization = kvp.Value;
                
                if (engine == null || engine.IsQueuedForDeletion())
                {
                    RemoveThrustVisualization(engine);
                    continue;
                }
            }
            
            var duration = Time.GetTicksMsec() - startTime;
            if (duration > ThrustVisualizationBudget)
            {
                GD.PrintErr($"EffectsManager: Thrust visualization exceeded budget - {duration:F2}ms (target: {ThrustVisualizationBudget:F2}ms)");
            }
        }
        
        /// <summary>
        /// Clean up inactive effects and return objects to pools
        /// </summary>
        private void CleanupInactiveEffects()
        {
            // This would be called periodically to clean up old effects
            // For now, effects are cleaned up when engines are removed
        }
        
        /// <summary>
        /// Remove engine exhaust effect
        /// </summary>
        /// <param name="engine">Engine to remove effect for</param>
        public void RemoveEngineEffect(Engine engine)
        {
            if (_engineEffects.TryGetValue(engine, out var effect))
            {
                // Return objects to pools
                if (effect.ParticleSystem != null)
                {
                    effect.ParticleSystem.GetParent()?.RemoveChild(effect.ParticleSystem);
                    _exhaustParticlePool.Return(effect.ParticleSystem);
                }
                
                if (effect.ExhaustLight != null)
                {
                    effect.ExhaustLight.GetParent()?.RemoveChild(effect.ExhaustLight);
                    _exhaustLightPool.Return(effect.ExhaustLight);
                }
                
                _engineEffects.Remove(engine);
            }
        }
        
        /// <summary>
        /// Remove thrust visualization
        /// </summary>
        /// <param name="engine">Engine to remove visualization for</param>
        public void RemoveThrustVisualization(Engine engine)
        {
            if (_thrustVisualizations.TryGetValue(engine, out var visualization))
            {
                // Return arrow to pool
                if (visualization.ThrustArrow != null)
                {
                    visualization.ThrustArrow.GetParent()?.RemoveChild(visualization.ThrustArrow);
                    _thrustArrowPool.Return(visualization.ThrustArrow);
                }
                
                _thrustVisualizations.Remove(engine);
            }
        }
        
        /// <summary>
        /// Create exhaust particle system
        /// </summary>
        /// <returns>Configured particle system</returns>
        private GpuParticles3D CreateExhaustParticleSystem()
        {
            var particles = new GpuParticles3D();
            particles.ProcessMaterial = CreateExhaustMaterial();
            particles.Amount = 100;
            particles.Lifetime = 2.0f;
            particles.Emitting = false;
            return particles;
        }
        
        /// <summary>
        /// Create exhaust particle material
        /// </summary>
        /// <returns>Configured particle material</returns>
        private ParticleProcessMaterial CreateExhaustMaterial()
        {
            var material = new ParticleProcessMaterial();
            material.Direction = Vector3.Down;
            material.InitialVelocity = new Vector2(15.0f, 0);
            material.AngularVelocity = new Vector2(30.0f, 0);
            material.Gravity = Vector3.Down * 2.0f;
            material.Scale = new Vector2(1.0f, 1.0f);
            material.Color = new Color(1.0f, 0.8f, 0.2f, 0.8f); // Orange flame
            return material;
        }
        
        /// <summary>
        /// Create thrust arrow visualization
        /// </summary>
        /// <returns>Configured thrust arrow</returns>
        private MeshInstance3D CreateThrustArrow()
        {
            var arrow = new MeshInstance3D();
            arrow.Mesh = new CylinderMesh
            {
                TopRadius = 0.0f,
                BottomRadius = 0.3f,
                Height = 1.0f
            };
            
            var material = new StandardMaterial3D();
            material.AlbedoColor = new Color(0, 1, 0); // Green
            arrow.SetSurfaceOverrideMaterial(0, material);
            
            return arrow;
        }
        
        /// <summary>
        /// Create exhaust light
        /// </summary>
        /// <returns>Configured exhaust light</returns>
        private SpotLight3D CreateExhaustLight()
        {
            var light = new SpotLight3D();
            light.LightEnergy = 0.0f;
            light.SpotAngle = 45.0f;
            light.LightColor = new Color(1.0f, 0.5f, 0.0f); // Orange
            return light;
        }
        
        /// <summary>
        /// Get effects manager performance metrics
        /// </summary>
        /// <returns>Performance statistics</returns>
        public EffectsManagerMetrics GetPerformanceMetrics()
        {
            return new EffectsManagerMetrics
            {
                LastUpdateTime = _lastUpdateTime,
                ActiveEffects = _activeEffects,
                ExhaustParticlePoolSize = _exhaustParticlePool.Count,
                ThrustArrowPoolSize = _thrustArrowPool.Count,
                ExhaustLightPoolSize = _exhaustLightPool.Count,
                UpdateBudget = EffectsUpdateBudget,
                VisualizationBudget = ThrustVisualizationBudget,
                IsWithinBudget = _lastUpdateTime <= EffectsUpdateBudget
            };
        }
        
        /// <summary>
        /// Cleanup on exit
        /// </summary>
        public override void _ExitTree()
        {
            // Clean up all effects
            foreach (var engine in _engineEffects.Keys.ToList())
            {
                RemoveEngineEffect(engine);
            }
            
            foreach (var engine in _thrustVisualizations.Keys.ToList())
            {
                RemoveThrustVisualization(engine);
            }
            
            if (_instance == this)
            {
                _instance = null;
            }
            
            base._ExitTree();
        }
    }
    
    /// <summary>
    /// Exhaust effect data for an engine
    /// </summary>
    public class ExhaustEffect
    {
        private GpuParticles3D? _particleSystem;
        public GpuParticles3D ParticleSystem 
        { 
            get => _particleSystem ?? throw new InvalidOperationException("ParticleSystem not initialized in ExhaustEffect");
            set => _particleSystem = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        private SpotLight3D? _exhaustLight;
        public SpotLight3D ExhaustLight 
        { 
            get => _exhaustLight ?? throw new InvalidOperationException("ExhaustLight not initialized in ExhaustEffect");
            set => _exhaustLight = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        private Engine? _engine;
        public Engine Engine 
        { 
            get => _engine ?? throw new InvalidOperationException("Engine not initialized in ExhaustEffect");
            set => _engine = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public bool IsActive { get; set; } = false;
        
        /// <summary>
        /// Constructor requiring all components to be set
        /// </summary>
        public ExhaustEffect(GpuParticles3D particleSystem, SpotLight3D exhaustLight, Engine engine)
        {
            ParticleSystem = particleSystem;
            ExhaustLight = exhaustLight;
            Engine = engine;
        }
        
        /// <summary>
        /// Checks if all components are valid
        /// </summary>
        public bool AreComponentsValid => SafeOperations.IsValid(_particleSystem, "ParticleSystem") &&
                                         SafeOperations.IsValid(_exhaustLight, "ExhaustLight") &&
                                         SafeOperations.IsValid(_engine, "Engine");
    }
    
    /// <summary>
    /// Thrust visualization data for an engine
    /// </summary>
    public class ThrustVisualization
    {
        private MeshInstance3D? _thrustArrow;
        public MeshInstance3D ThrustArrow 
        { 
            get => _thrustArrow ?? throw new InvalidOperationException("ThrustArrow not initialized in ThrustVisualization");
            set => _thrustArrow = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        private Engine? _engine;
        public Engine Engine 
        { 
            get => _engine ?? throw new InvalidOperationException("Engine not initialized in ThrustVisualization");
            set => _engine = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public bool IsVisible { get; set; } = false;
        
        /// <summary>
        /// Constructor requiring all components to be set
        /// </summary>
        public ThrustVisualization(MeshInstance3D thrustArrow, Engine engine)
        {
            ThrustArrow = thrustArrow;
            Engine = engine;
        }
        
        /// <summary>
        /// Checks if all components are valid
        /// </summary>
        public bool AreComponentsValid => SafeOperations.IsValid(_thrustArrow, "ThrustArrow") &&
                                         SafeOperations.IsValid(_engine, "Engine");
    }
    
    /// <summary>
    /// Simple object pool for performance optimization
    /// </summary>
    /// <typeparam name="T">Type of object to pool</typeparam>
    public class ObjectPool<T> where T : Node, new()
    {
        private readonly Queue<T> _objects = new();
        
        public int Count => _objects.Count;
        
        public T Get()
        {
            if (_objects.Count > 0)
            {
                return _objects.Dequeue();
            }
            
            return new T();
        }
        
        public void Return(T obj)
        {
            if (obj != null)
            {
                // Reset object state for specific types
                if (obj is Node3D node)
                {
                    node.Visible = false;
                }
                if (obj is GpuParticles3D particles)
                {
                    particles.Emitting = false;
                }
                if (obj is SpotLight3D light)
                {
                    light.LightEnergy = 0.0f;
                }
                
                _objects.Enqueue(obj);
            }
        }
    }
    
    /// <summary>
    /// Effects manager performance metrics
    /// </summary>
    public struct EffectsManagerMetrics
    {
        public double LastUpdateTime;
        public int ActiveEffects;
        public int ExhaustParticlePoolSize;
        public int ThrustArrowPoolSize;
        public int ExhaustLightPoolSize;
        public double UpdateBudget;
        public double VisualizationBudget;
        public bool IsWithinBudget;
    }
}