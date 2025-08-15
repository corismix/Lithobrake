using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Anti-wobble system for dynamic joint stiffening based on atmospheric conditions.
    /// Prevents structural wobble in tall rocket stacks while maintaining realistic physics.
    /// Uses Q_ENABLE=12kPa and Q_DISABLE=8kPa thresholds with TAU=0.3s smooth transitions.
    /// </summary>
    public class AntiWobbleSystem
    {
        // Anti-wobble thresholds and parameters
        private const double QEnable = 12000.0;    // 12 kPa - enable anti-wobble
        private const double QDisable = 8000.0;    // 8 kPa - disable anti-wobble (hysteresis)
        private const float Tau = 0.3f;            // 0.3s time constant for exponential smoothing
        private const float MaxStiffnessMultiplier = 5.0f;  // Maximum 5x stiffness increase
        private const int LongChainThreshold = 5;   // >5 parts considered long chain
        
        // State tracking
        private bool _antiWobbleEnabled = false;
        private readonly Dictionary<int, JointStiffnessState> _jointStiffness = new();
        private readonly Dictionary<int, VirtualStrut> _virtualStruts = new();
        private int _nextVirtualStrutId = 0;
        
        // Performance tracking
        private double _lastProcessTime = 0.0;
        
        /// <summary>
        /// Current anti-wobble enabled state
        /// </summary>
        public bool IsEnabled => _antiWobbleEnabled;
        
        /// <summary>
        /// Number of joints with active stiffening
        /// </summary>
        public int ActiveStiffenedJoints => _jointStiffness.Count(kvp => kvp.Value.CurrentMultiplier > 1.01f);
        
        /// <summary>
        /// Number of active virtual struts
        /// </summary>
        public int ActiveVirtualStruts => _virtualStruts.Count(kvp => kvp.Value.IsActive);
        
        /// <summary>
        /// Process anti-wobble system for a vessel
        /// </summary>
        /// <param name="vessel">Target vessel</param>
        /// <param name="altitude">Current altitude in meters</param>
        /// <param name="velocity">Current velocity vector</param>
        /// <param name="deltaTime">Frame delta time</param>
        public void ProcessVessel(PhysicsVessel vessel, double altitude, Double3 velocity, float deltaTime)
        {
            var startTime = Time.GetUnixTimeFromSystem();
            
            // Calculate dynamic pressure
            var dynamicPressure = AtmosphericConditions.GetDynamicPressure(altitude, velocity);
            
            // Update anti-wobble state with hysteresis
            var previousState = _antiWobbleEnabled;
            _antiWobbleEnabled = AtmosphericConditions.ShouldEnableAntiWobble(dynamicPressure, _antiWobbleEnabled);
            
            // If state changed, log it
            if (previousState != _antiWobbleEnabled)
            {
                GD.Print($"AntiWobble: {(_antiWobbleEnabled ? "ENABLED" : "DISABLED")} at Q={dynamicPressure/1000.0:F1}kPa");
            }
            
            // Update joint stiffening
            UpdateJointStiffening(vessel, dynamicPressure, deltaTime);
            
            // Update virtual struts for long chains
            UpdateVirtualStruts(vessel, deltaTime);
            
            _lastProcessTime = (Time.GetUnixTimeFromSystem() - startTime) * 1000.0; // Convert to ms
        }
        
        /// <summary>
        /// Update joint stiffening based on current conditions
        /// </summary>
        private void UpdateJointStiffening(PhysicsVessel vessel, double dynamicPressure, float deltaTime)
        {
            var vesselJoints = GetVesselJoints(vessel);
            if (vesselJoints.Count == 0) return;
            
            // Calculate wobble factor based on dynamic pressure and chain analysis
            var wobbleFactor = CalculateWobbleFactor(vessel, dynamicPressure);
            var targetStiffnessMultiplier = _antiWobbleEnabled ? 
                Mathf.Clamp(wobbleFactor, 1.0f, MaxStiffnessMultiplier) : 1.0f;
            
            // Update each joint's stiffness with smooth exponential transitions
            foreach (var jointInfo in vesselJoints)
            {
                if (!_jointStiffness.TryGetValue(jointInfo.Id, out var stiffnessState))
                {
                    stiffnessState = new JointStiffnessState
                    {
                        OriginalTuning = jointInfo.Tuning,
                        CurrentMultiplier = 1.0f,
                        TargetMultiplier = 1.0f
                    };
                    _jointStiffness[jointInfo.Id] = stiffnessState;
                }
                
                // Update target multiplier
                stiffnessState.TargetMultiplier = targetStiffnessMultiplier;
                
                // Smooth exponential transition: x(t) = target + (current - target) * e^(-dt/τ)
                var alpha = 1.0f - Mathf.Exp(-deltaTime / Tau);
                var previousMultiplier = stiffnessState.CurrentMultiplier;
                stiffnessState.CurrentMultiplier = Mathf.Lerp(
                    stiffnessState.CurrentMultiplier, 
                    stiffnessState.TargetMultiplier, 
                    alpha
                );
                
                // Apply new stiffness if it changed significantly
                if (Mathf.Abs(stiffnessState.CurrentMultiplier - previousMultiplier) > 0.01f)
                {
                    var newTuning = stiffnessState.OriginalTuning.Scale(stiffnessState.CurrentMultiplier);
                    vessel.UpdateJointTuning(jointInfo.Id, newTuning);
                }
            }
            
            // Clean up stiffness states for removed joints (avoid LINQ allocations)
            var activeJointIds = new HashSet<int>();
            foreach (var joint in vesselJoints)
            {
                activeJointIds.Add(joint.Id);
            }
            
            var stiffnessKeysToRemove = new List<int>();
            foreach (var id in _jointStiffness.Keys)
            {
                if (!activeJointIds.Contains(id))
                {
                    stiffnessKeysToRemove.Add(id);
                }
            }
            
            foreach (var keyToRemove in stiffnessKeysToRemove)
            {
                _jointStiffness.Remove(keyToRemove);
            }
        }
        
        /// <summary>
        /// Calculate wobble factor based on dynamic pressure and vessel configuration
        /// </summary>
        private float CalculateWobbleFactor(PhysicsVessel vessel, double dynamicPressure)
        {
            if (!_antiWobbleEnabled) return 1.0f;
            
            // Base wobble factor from dynamic pressure
            var pressureFactor = (float)(dynamicPressure / QEnable); // Normalize to enable threshold
            
            // Chain length factor - longer chains need more stiffening
            var partCount = vessel.GetPartCount();
            var chainLengthFactor = 1.0f + (partCount / 30.0f); // Scale with part count up to 30 parts
            
            // Height factor - assume taller stacks are more wobble-prone
            // This is simplified; a real implementation would analyze the vessel structure
            var heightFactor = 1.0f + Mathf.Clamp((partCount - 10) / 20.0f, 0.0f, 1.0f);
            
            // Combine factors
            var wobbleFactor = 1.0f + (pressureFactor * chainLengthFactor * heightFactor);
            
            return Mathf.Clamp(wobbleFactor, 1.0f, MaxStiffnessMultiplier);
        }
        
        /// <summary>
        /// Update virtual struts for long vessel chains
        /// </summary>
        private void UpdateVirtualStruts(PhysicsVessel vessel, float deltaTime)
        {
            var partCount = vessel.GetPartCount();
            var needsVirtualStruts = _antiWobbleEnabled && partCount > LongChainThreshold;
            
            if (needsVirtualStruts)
            {
                // Create virtual struts if they don't exist
                CreateVirtualStrutsIfNeeded(vessel);
            }
            else
            {
                // Remove virtual struts if they're not needed
                RemoveVirtualStruts(vessel);
            }
            
            // Update existing virtual struts
            UpdateExistingVirtualStruts(deltaTime);
        }
        
        /// <summary>
        /// Create virtual struts for long chains if needed
        /// </summary>
        private void CreateVirtualStrutsIfNeeded(PhysicsVessel vessel)
        {
            var vesselJoints = GetVesselJoints(vessel);
            if (vesselJoints.Count == 0) return;
            
            // Simple heuristic: create virtual struts between parts that are far apart in the chain
            // This is simplified; a real implementation would analyze the vessel structure tree
            var partCount = vessel.GetPartCount();
            
            if (partCount > LongChainThreshold && _virtualStruts.Count == 0)
            {
                // For now, just track that we need virtual struts
                // The actual implementation would create additional joints between non-adjacent parts
                var virtualStrut = new VirtualStrut
                {
                    Id = _nextVirtualStrutId++,
                    IsActive = true,
                    CreationTime = Time.GetUnixTimeFromSystem(),
                    PartAId = 0, // Root part
                    PartBId = partCount - 1, // End part
                    StiffnessMultiplier = 2.0f
                };
                
                _virtualStruts[virtualStrut.Id] = virtualStrut;
                GD.Print($"AntiWobble: Created virtual strut {virtualStrut.Id} for {partCount}-part vessel");
            }
        }
        
        /// <summary>
        /// Remove virtual struts when not needed
        /// </summary>
        private void RemoveVirtualStruts(PhysicsVessel vessel)
        {
            if (_virtualStruts.Count == 0) return;
            
            // Avoid ToList() allocation by copying keys to array
            var strutsToRemove = new List<int>();
            foreach (var strutId in _virtualStruts.Keys)
            {
                strutsToRemove.Add(strutId);
            }
            
            foreach (var strutId in strutsToRemove)
            {
                _virtualStruts.Remove(strutId);
                GD.Print($"AntiWobble: Removed virtual strut {strutId}");
            }
        }
        
        /// <summary>
        /// Update existing virtual struts
        /// </summary>
        private void UpdateExistingVirtualStruts(float deltaTime)
        {
            // For each active virtual strut, ensure it's still valid and update its properties
            // Use foreach without LINQ to avoid allocation pressure
            foreach (var strut in _virtualStruts.Values)
            {
                if (!strut.IsActive)
                    continue;
                // Virtual struts would apply additional constraint forces here
                // For now, we just track their existence
            }
        }
        
        /// <summary>
        /// Get vessel joints from PhysicsVessel
        /// </summary>
        private List<JointInfo> GetVesselJoints(PhysicsVessel vessel)
        {
            var vesselJoints = vessel.GetJoints();
            var joints = new List<JointInfo>();
            
            foreach (var vesselJoint in vesselJoints)
            {
                joints.Add(new JointInfo
                {
                    Id = vesselJoint.Id,
                    Tuning = vesselJoint.Tuning,
                    IsActive = vesselJoint.IsActive
                });
            }
            
            return joints;
        }
        
        /// <summary>
        /// Reset anti-wobble system state
        /// </summary>
        public void Reset()
        {
            _antiWobbleEnabled = false;
            _jointStiffness.Clear();
            _virtualStruts.Clear();
            _nextVirtualStrutId = 0;
            GD.Print("AntiWobble: System reset");
        }
        
        /// <summary>
        /// Ensure anti-wobble system maintains stability across floating origin shifts.
        /// The anti-wobble system primarily operates through joint stiffness modifications
        /// which are handled by the PhysicsVessel during origin shifts.
        /// </summary>
        /// <param name="vessel">The vessel being processed</param>
        /// <param name="deltaPosition">The origin shift delta (not directly used)</param>
        public void HandleOriginShift(PhysicsVessel vessel, Double3 deltaPosition)
        {
            // The anti-wobble system doesn't need to directly handle coordinate shifts
            // since it works through joint tuning parameters that are maintained by 
            // the PhysicsVessel during origin shifts.
            
            // However, we can validate that our state remains consistent
            var vesselJoints = GetVesselJoints(vessel);
            
            // Build active joint IDs set without LINQ to avoid allocations
            var activeJointIds = new HashSet<int>();
            foreach (var joint in vesselJoints)
            {
                activeJointIds.Add(joint.Id);
            }
            
            // Clean up any stale joint stiffness states without LINQ
            var staleStiffnessIds = new List<int>();
            foreach (var id in _jointStiffness.Keys)
            {
                if (!activeJointIds.Contains(id))
                {
                    staleStiffnessIds.Add(id);
                }
            }
            foreach (var staleId in staleStiffnessIds)
            {
                _jointStiffness.Remove(staleId);
            }
            
            // Virtual struts maintain their relative positioning automatically
            // since they reference part IDs, not absolute coordinates
            
            GD.Print($"AntiWobble: Maintained stability across origin shift (delta: {deltaPosition.Length:F1}m)");
        }
        
        /// <summary>
        /// Get performance metrics
        /// </summary>
        public AntiWobbleMetrics GetMetrics()
        {
            return new AntiWobbleMetrics
            {
                IsEnabled = _antiWobbleEnabled,
                ProcessTimeMs = _lastProcessTime,
                ActiveStiffenedJoints = ActiveStiffenedJoints,
                ActiveVirtualStruts = ActiveVirtualStruts,
                MemoryUsageBytes = EstimateMemoryUsage()
            };
        }
        
        /// <summary>
        /// Estimate memory usage of the anti-wobble system
        /// </summary>
        private int EstimateMemoryUsage()
        {
            // Rough estimate: each joint stiffness state ~100 bytes, virtual strut ~50 bytes
            return (_jointStiffness.Count * 100) + (_virtualStruts.Count * 50) + 1000; // Base overhead
        }
    }
    
    /// <summary>
    /// Joint stiffness state for smooth transitions
    /// </summary>
    public class JointStiffnessState
    {
        public JointTuning OriginalTuning;
        public float CurrentMultiplier = 1.0f;
        public float TargetMultiplier = 1.0f;
    }
    
    /// <summary>
    /// Virtual strut for long chain stability
    /// </summary>
    public class VirtualStrut
    {
        public int Id;
        public bool IsActive;
        public double CreationTime;
        public int PartAId;
        public int PartBId;
        public float StiffnessMultiplier = 1.0f;
    }
    
    /// <summary>
    /// Simplified joint info for anti-wobble processing
    /// </summary>
    public class JointInfo
    {
        public int Id;
        public JointTuning Tuning;
        public bool IsActive;
    }
    
    /// <summary>
    /// Anti-wobble system performance metrics
    /// </summary>
    public struct AntiWobbleMetrics
    {
        public bool IsEnabled;
        public double ProcessTimeMs;
        public int ActiveStiffenedJoints;
        public int ActiveVirtualStruts;
        public int MemoryUsageBytes;
        
        public bool MeetsPerformanceTargets => ProcessTimeMs < 0.5; // <0.5ms target
    }
}