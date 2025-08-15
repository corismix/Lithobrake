using Godot;
using System;
using System.Collections.Generic;

namespace Lithobrake.Core
{
    /// <summary>
    /// Heating visualization system implementing red glow effects scaled by Q * velocity.
    /// Provides atmospheric entry heating visualization without damage mechanics,
    /// using particle effects and shader-based glow for realistic atmospheric flight feedback.
    /// Integrates with EffectsManager and dynamic pressure system for performance optimization.
    /// </summary>
    public partial class HeatingEffects : Node
    {
        // Singleton pattern for global access
        private static HeatingEffects? _instance;
        public static HeatingEffects Instance => _instance ?? throw new InvalidOperationException("HeatingEffects not initialized");
        
        // Heating effect tracking
        private readonly Dictionary<Part, HeatingEffect> _partHeatingEffects = new();
        private readonly Dictionary<PhysicsVessel, VesselHeatingData> _vesselHeatingData = new();
        
        // Object pools for performance optimization (similar to EffectsManager pattern)
        private readonly ObjectPool<GpuParticles3D> _heatingParticlePool = new();
        private readonly ObjectPool<SpotLight3D> _heatingGlowPool = new();
        
        // Heating effect constants
        private const double HEATING_THRESHOLD = 1000.0; // Minimum heating intensity to show effects
        private const double MAX_HEATING_INTENSITY = 100000.0; // Maximum heating for scaling
        private const float MAX_GLOW_INTENSITY = 3.0f; // Maximum glow light intensity
        private const float MAX_PARTICLE_SCALE = 2.0f; // Maximum particle system scale
        private const float MIN_GLOW_INTENSITY = 0.1f; // Minimum visible glow
        
        // Different part materials have different heating characteristics
        public static class HeatingCoefficients
        {
            public const double Aluminum = 1.0; // Standard heating
            public const double Steel = 0.8; // Better heat resistance
            public const double Titanium = 0.6; // High temperature resistance
            public const double Carbon = 1.2; // Burns more visibly
            public const double Ceramic = 0.4; // Heat shield material
        }
        
        // Performance tracking
        private double _lastUpdateTime = 0.0;
        private int _activeHeatingEffects = 0;
        
        // Performance targets from current-task.md
        private const double HEATING_EFFECTS_BUDGET = 0.5; // ms per frame for all parts
        
        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                InitializeObjectPools();
                GD.Print("HeatingEffects: Initialized with red glow effects and particle heating visualization");
            }
            else
            {
                GD.PrintErr("HeatingEffects: Multiple instances detected!");
                QueueFree();
            }
        }
        
        /// <summary>
        /// Initialize object pools for performance optimization
        /// </summary>
        private void InitializeObjectPools()
        {
            // Pre-populate pools with heating effect objects
            for (int i = 0; i < 20; i++) // Support heating effects for 20 parts simultaneously
            {
                _heatingParticlePool.Return(CreateHeatingParticleSystem());
                _heatingGlowPool.Return(CreateHeatingGlow());
            }
        }
        
        /// <summary>
        /// Update vessel heating effects based on heating intensity
        /// Called by DynamicPressure system with Q * velocity scaling
        /// </summary>
        /// <param name="vessel">Vessel to update heating for</param>
        /// <param name="heatingIntensity">Heating intensity from Q * velocity</param>
        public static void UpdateVesselHeating(PhysicsVessel vessel, double heatingIntensity)
        {
            if (vessel?.Parts == null || Instance == null)
                return;
            
            var startTime = Time.GetTicksMsec();
            
            // Get or create vessel heating data
            if (!Instance._vesselHeatingData.TryGetValue(vessel, out var vesselData))
            {
                vesselData = new VesselHeatingData();
                Instance._vesselHeatingData[vessel] = vesselData;
            }
            
            vesselData.Update(heatingIntensity);
            
            // Update heating effects for each part
            foreach (var part in vessel.Parts)
            {
                if (part != null)
                {
                    UpdatePartHeating(part, heatingIntensity, vesselData);
                }
            }
            
            // Performance monitoring
            Instance._lastUpdateTime = Time.GetTicksMsec() - startTime;
            if (Instance._lastUpdateTime > HEATING_EFFECTS_BUDGET)
            {
                GD.PrintErr($"HeatingEffects: Update exceeded budget - {Instance._lastUpdateTime:F2}ms (target: {HEATING_EFFECTS_BUDGET:F2}ms)");
            }
        }
        
        /// <summary>
        /// Update heating effects for a single part
        /// </summary>
        /// <param name="part">Part to update heating for</param>
        /// <param name="vesselHeatingIntensity">Overall vessel heating intensity</param>
        /// <param name="vesselData">Vessel heating data for context</param>
        private static void UpdatePartHeating(Part part, double vesselHeatingIntensity, VesselHeatingData vesselData)
        {
            if (Instance == null)
                return;
            
            // Calculate part-specific heating intensity
            var partHeatingIntensity = CalculatePartHeatingIntensity(part, vesselHeatingIntensity);
            
            // Get or create part heating effect
            if (!Instance._partHeatingEffects.TryGetValue(part, out var effect))
            {
                effect = CreatePartHeatingEffect(part);
                Instance._partHeatingEffects[part] = effect;
            }
            
            // Update heating effect based on intensity
            UpdateHeatingEffect(effect, part, partHeatingIntensity);
        }
        
        /// <summary>
        /// Calculate part-specific heating intensity based on shape and material
        /// </summary>
        /// <param name="part">Part to calculate heating for</param>
        /// <param name="baseHeatingIntensity">Base heating intensity from vessel</param>
        /// <returns>Part-specific heating intensity</returns>
        private static double CalculatePartHeatingIntensity(Part part, double baseHeatingIntensity)
        {
            // Material coefficient affects heating visibility
            var materialCoefficient = GetPartMaterialCoefficient(part);
            
            // Shape coefficient - blunt objects heat more than streamlined
            var shapeCoefficient = GetPartShapeCoefficient(part);
            
            // Position coefficient - leading parts heat more
            var positionCoefficient = GetPartPositionCoefficient(part);
            
            return baseHeatingIntensity * materialCoefficient * shapeCoefficient * positionCoefficient;
        }
        
        /// <summary>
        /// Get material heating coefficient for part
        /// </summary>
        /// <param name="part">Part to get coefficient for</param>
        /// <returns>Material heating coefficient</returns>
        private static double GetPartMaterialCoefficient(Part part)
        {
            // For now, use part type as a proxy for material
            // In the future, parts could have explicit material properties
            return part.Type switch
            {
                PartType.Command => HeatingCoefficients.Aluminum,
                PartType.FuelTank => HeatingCoefficients.Aluminum,
                PartType.Engine => HeatingCoefficients.Steel,
                PartType.Structural => HeatingCoefficients.Steel,
                PartType.Aerodynamic => HeatingCoefficients.Carbon,
                PartType.Utility => HeatingCoefficients.Aluminum, // Solar panels and electrical
                PartType.Science => HeatingCoefficients.Titanium, // Scientific equipment
                PartType.LandingGear => HeatingCoefficients.Steel, // Landing gear
                PartType.Coupling => HeatingCoefficients.Steel, // Docking ports
                PartType.Cargo => HeatingCoefficients.Ceramic, // Cargo bays often have thermal protection
                _ => HeatingCoefficients.Aluminum
            };
        }
        
        /// <summary>
        /// Get shape heating coefficient for part
        /// </summary>
        /// <param name="part">Part to get coefficient for</param>
        /// <returns>Shape heating coefficient</returns>
        private static double GetPartShapeCoefficient(Part part)
        {
            // Blunt parts heat more than streamlined parts
            return part.BaseDragCoefficient switch
            {
                <= 0.3 => 0.5, // Streamlined parts heat less
                <= 0.6 => 0.8, // Moderately streamlined
                <= 1.0 => 1.0, // Normal heating
                _ => 1.2 // Blunt parts heat more
            };
        }
        
        /// <summary>
        /// Get position heating coefficient for part (leading parts heat more)
        /// </summary>
        /// <param name="part">Part to get coefficient for</param>
        /// <returns>Position heating coefficient</returns>
        private static double GetPartPositionCoefficient(Part part)
        {
            // For now, simplified - could be made more sophisticated with vessel structure analysis
            // Command pods (typically at front) heat more
            if (part.Type == PartType.Command)
                return 1.5;
            
            return 1.0;
        }
        
        /// <summary>
        /// Create heating effect for part
        /// </summary>
        /// <param name="part">Part to create effect for</param>
        /// <returns>Heating effect data</returns>
        private static HeatingEffect? CreatePartHeatingEffect(Part part)
        {
            if (Instance == null)
                return null;
            
            var particles = Instance._heatingParticlePool.Get();
            var glow = Instance._heatingGlowPool.Get();
            
            // Position effects at part location
            particles.GlobalPosition = part.GlobalPosition;
            glow.GlobalPosition = part.GlobalPosition;
            
            // Initial setup
            particles.Emitting = false;
            glow.LightEnergy = 0.0f;
            
            // Add to scene
            Instance.AddChild(particles);
            Instance.AddChild(glow);
            
            return new HeatingEffect(particles, glow, part)
            {
                IsActive = false,
                CurrentIntensity = 0.0
            };
        }
        
        /// <summary>
        /// Update heating effect based on intensity
        /// </summary>
        /// <param name="effect">Heating effect to update</param>
        /// <param name="part">Part providing data</param>
        /// <param name="heatingIntensity">Heating intensity</param>
        private static void UpdateHeatingEffect(HeatingEffect effect, Part part, double heatingIntensity)
        {
            var shouldBeActive = heatingIntensity > HEATING_THRESHOLD;
            
            if (shouldBeActive != effect.IsActive)
            {
                effect.IsActive = shouldBeActive;
                effect.ParticleSystem.Emitting = shouldBeActive;
                
                if (Instance != null)
                {
                    if (shouldBeActive)
                        Instance._activeHeatingEffects++;
                    else
                        Instance._activeHeatingEffects--;
                }
            }
            
            if (shouldBeActive)
            {
                effect.CurrentIntensity = heatingIntensity;
                
                // Scale effects by heating intensity
                var normalizedIntensity = Math.Clamp(heatingIntensity / MAX_HEATING_INTENSITY, 0.0, 1.0);
                
                // Update particle system
                UpdateHeatingParticles(effect.ParticleSystem, normalizedIntensity);
                
                // Update glow light
                UpdateHeatingGlow(effect.GlowLight, normalizedIntensity);
                
                // Update positions to follow part
                effect.ParticleSystem.GlobalPosition = part.GlobalPosition;
                effect.GlowLight.GlobalPosition = part.GlobalPosition;
            }
            else
            {
                effect.CurrentIntensity = 0.0;
                effect.GlowLight.LightEnergy = 0.0f;
            }
        }
        
        /// <summary>
        /// Update heating particle system based on intensity
        /// </summary>
        /// <param name="particles">Particle system to update</param>
        /// <param name="normalizedIntensity">Heating intensity 0-1</param>
        private static void UpdateHeatingParticles(GpuParticles3D particles, double normalizedIntensity)
        {
            var material = particles.ProcessMaterial as ParticleProcessMaterial;
            if (material != null)
            {
                // Scale particle size and emission rate by heating intensity
                var particleScale = MIN_GLOW_INTENSITY + (float)(normalizedIntensity * (MAX_PARTICLE_SCALE - MIN_GLOW_INTENSITY));
                material.Scale = new Vector2(particleScale, particleScale);
                
                // Heating color progression: red → orange → yellow → white
                var heatingColor = GetHeatingColor(normalizedIntensity);
                material.Color = heatingColor;
                
                // Increase emission rate with intensity
                particles.Amount = (int)(20 + normalizedIntensity * 80); // 20-100 particles
            }
        }
        
        /// <summary>
        /// Update heating glow light based on intensity
        /// </summary>
        /// <param name="glow">Glow light to update</param>
        /// <param name="normalizedIntensity">Heating intensity 0-1</param>
        private static void UpdateHeatingGlow(SpotLight3D glow, double normalizedIntensity)
        {
            // Intensity with minimum visible threshold
            var lightIntensity = MIN_GLOW_INTENSITY + (float)(normalizedIntensity * (MAX_GLOW_INTENSITY - MIN_GLOW_INTENSITY));
            glow.LightEnergy = lightIntensity;
            
            // Heating color progression
            var heatingColor = GetHeatingColor(normalizedIntensity);
            glow.LightColor = heatingColor;
            
            // Adjust spotlight angle based on intensity (more spread = more heating)
            glow.SpotAngle = (float)(30.0 + normalizedIntensity * 30.0); // 30-60 degrees
        }
        
        /// <summary>
        /// Get heating color based on intensity (red → orange → yellow → white)
        /// </summary>
        /// <param name="normalizedIntensity">Heating intensity 0-1</param>
        /// <returns>Heating color</returns>
        private static Color GetHeatingColor(double normalizedIntensity)
        {
            var intensity = (float)Math.Clamp(normalizedIntensity, 0.0, 1.0);
            
            if (intensity < 0.33f)
            {
                // Red to orange
                var t = intensity / 0.33f;
                return new Color(1.0f, t * 0.5f, 0.0f, 0.8f);
            }
            else if (intensity < 0.66f)
            {
                // Orange to yellow
                var t = (intensity - 0.33f) / 0.33f;
                return new Color(1.0f, 0.5f + t * 0.5f, t * 0.3f, 0.9f);
            }
            else
            {
                // Yellow to white
                var t = (intensity - 0.66f) / 0.34f;
                return new Color(1.0f, 1.0f, 0.3f + t * 0.7f, 1.0f);
            }
        }
        
        /// <summary>
        /// Remove heating effect for part
        /// </summary>
        /// <param name="part">Part to remove effect for</param>
        public static void RemovePartHeatingEffect(Part part)
        {
            if (Instance == null || !Instance._partHeatingEffects.TryGetValue(part, out var effect))
                return;
            
            // Return objects to pools
            if (effect.ParticleSystem != null)
            {
                effect.ParticleSystem.GetParent()?.RemoveChild(effect.ParticleSystem);
                Instance._heatingParticlePool.Return(effect.ParticleSystem);
            }
            
            if (effect.GlowLight != null)
            {
                effect.GlowLight.GetParent()?.RemoveChild(effect.GlowLight);
                Instance._heatingGlowPool.Return(effect.GlowLight);
            }
            
            Instance._partHeatingEffects.Remove(part);
        }
        
        /// <summary>
        /// Clear vessel heating tracking (for vessel destruction)
        /// </summary>
        /// <param name="vessel">Vessel to clear tracking for</param>
        public static void ClearVesselHeating(PhysicsVessel vessel)
        {
            if (Instance == null)
                return;
            
            Instance._vesselHeatingData.Remove(vessel);
            
            // Remove all part heating effects for this vessel
            if (vessel.Parts != null)
            {
                foreach (var part in vessel.Parts)
                {
                    if (part != null)
                    {
                        RemovePartHeatingEffect(part);
                    }
                }
            }
        }
        
        /// <summary>
        /// Create heating particle system
        /// </summary>
        /// <returns>Configured heating particle system</returns>
        private GpuParticles3D CreateHeatingParticleSystem()
        {
            var particles = new GpuParticles3D();
            particles.ProcessMaterial = CreateHeatingParticleMaterial();
            particles.Amount = 50;
            particles.Lifetime = 1.0f;
            particles.Emitting = false;
            return particles;
        }
        
        /// <summary>
        /// Create heating particle material
        /// </summary>
        /// <returns>Configured heating particle material</returns>
        private ParticleProcessMaterial CreateHeatingParticleMaterial()
        {
            var material = new ParticleProcessMaterial();
            material.Direction = Vector3.Up; // Heat rises
            material.InitialVelocity = new Vector2(2.0f, 0); // Slow upward movement
            material.AngularVelocity = new Vector2(45.0f, 0); // Rotation
            material.Gravity = Vector3.Up * 1.0f; // Heat rises against gravity
            material.Scale = new Vector2(0.5f, 0.5f);
            material.Color = new Color(1.0f, 0.0f, 0.0f, 0.6f); // Red heating glow
            return material;
        }
        
        /// <summary>
        /// Create heating glow light
        /// </summary>
        /// <returns>Configured heating glow light</returns>
        private SpotLight3D CreateHeatingGlow()
        {
            var light = new SpotLight3D();
            light.LightEnergy = 0.0f;
            light.SpotAngle = 45.0f;
            light.LightColor = new Color(1.0f, 0.0f, 0.0f); // Red glow
            // Note: LightAttenuation property may not exist in Godot 4.4.1
            // Using default attenuation for now
            return light;
        }
        
        /// <summary>
        /// Get heating effects performance metrics
        /// </summary>
        /// <returns>Performance statistics</returns>
        public HeatingEffectsPerformanceMetrics GetPerformanceMetrics()
        {
            return new HeatingEffectsPerformanceMetrics
            {
                LastUpdateTime = _lastUpdateTime,
                ActiveHeatingEffects = _activeHeatingEffects,
                TrackedVessels = _vesselHeatingData.Count,
                TrackedParts = _partHeatingEffects.Count,
                HeatingParticlePoolSize = _heatingParticlePool.Count,
                HeatingGlowPoolSize = _heatingGlowPool.Count,
                UpdateBudget = HEATING_EFFECTS_BUDGET,
                IsWithinBudget = _lastUpdateTime <= HEATING_EFFECTS_BUDGET
            };
        }
        
        /// <summary>
        /// Cleanup on exit
        /// </summary>
        public override void _ExitTree()
        {
            // Clean up all effects
            foreach (var part in _partHeatingEffects.Keys)
            {
                RemovePartHeatingEffect(part);
            }
            
            if (_instance == this)
            {
                _instance = null;
            }
            
            base._ExitTree();
        }
    }
    
    /// <summary>
    /// Heating effect data for a part
    /// </summary>
    public class HeatingEffect
    {
        private GpuParticles3D? _particleSystem;
        public GpuParticles3D ParticleSystem 
        { 
            get => _particleSystem ?? throw new InvalidOperationException("ParticleSystem not initialized in HeatingEffect");
            set => _particleSystem = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        private SpotLight3D? _glowLight;
        public SpotLight3D GlowLight 
        { 
            get => _glowLight ?? throw new InvalidOperationException("GlowLight not initialized in HeatingEffect");
            set => _glowLight = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        private Part? _part;
        public Part Part 
        { 
            get => _part ?? throw new InvalidOperationException("Part not initialized in HeatingEffect");
            set => _part = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public bool IsActive { get; set; } = false;
        public double CurrentIntensity { get; set; } = 0.0;
        
        /// <summary>
        /// Constructor requiring all components to be set
        /// </summary>
        public HeatingEffect(GpuParticles3D particleSystem, SpotLight3D glowLight, Part part)
        {
            ParticleSystem = particleSystem;
            GlowLight = glowLight;
            Part = part;
        }
        
        /// <summary>
        /// Checks if all components are valid
        /// </summary>
        public bool AreComponentsValid => SafeOperations.IsValid(_particleSystem, "ParticleSystem") &&
                                         SafeOperations.IsValid(_glowLight, "GlowLight") &&
                                         SafeOperations.IsValid(_part, "Part");
    }
    
    /// <summary>
    /// Vessel heating tracking data
    /// </summary>
    public class VesselHeatingData
    {
        public double CurrentHeatingIntensity { get; private set; } = 0.0;
        public double MaxHeatingIntensity { get; private set; } = 0.0;
        public double LastHeatingIntensity { get; private set; } = 0.0;
        
        public void Update(double heatingIntensity)
        {
            LastHeatingIntensity = CurrentHeatingIntensity;
            CurrentHeatingIntensity = heatingIntensity;
            
            if (heatingIntensity > MaxHeatingIntensity)
            {
                MaxHeatingIntensity = heatingIntensity;
            }
        }
    }
    
    /// <summary>
    /// Performance metrics for heating effects system
    /// </summary>
    public struct HeatingEffectsPerformanceMetrics
    {
        public double LastUpdateTime;
        public int ActiveHeatingEffects;
        public int TrackedVessels;
        public int TrackedParts;
        public int HeatingParticlePoolSize;
        public int HeatingGlowPoolSize;
        public double UpdateBudget;
        public bool IsWithinBudget;
    }
}