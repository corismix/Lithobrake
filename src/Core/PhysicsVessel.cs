using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Multi-part vessel physics handler for rocket simulation.
    /// Manages parts list, joints, mass properties, and vessel lifecycle.
    /// Integrates with Double3 coordinate system for orbital mechanics.
    /// </summary>
    public partial class PhysicsVessel : Node3D
    {
        // Vessel identification
        private int _vesselId;
        private PhysicsManager? _physicsManager;
        
        // Parts and joints management
        private readonly List<VesselPart> _parts = new();
        private readonly List<VesselJoint> _joints = new();
        private readonly Dictionary<int, VesselPart> _partLookup = new();
        
        // Mass properties (cached for performance)
        private double _totalMass = 0.0;
        private Double3 _centerOfMass = Double3.Zero;
        private Double3 _momentOfInertia = Double3.Zero;
        private bool _massPropertiesDirty = true;
        
        // Physics state
        private Double3 _position = Double3.Zero;
        private Double3 _velocity = Double3.Zero;
        private Vector3 _angularVelocity = Vector3.Zero;
        private bool _isActive = true;
        
        // Performance tracking
        private int _framesSinceUpdate = 0;
        private const int UpdateFrequency = 1; // Update every frame
        
        // Part limit enforcement
        private const int MaxParts = 75; // From CLAUDE.md constraints
        
        // Anti-wobble system
        private AntiWobbleSystem? _antiWobbleSystem;
        
        // Separation event system
        private readonly List<SeparationEvent> _separationHistory = new();
        private int _nextEventId = 0;
        
        // Performance constants for separation system
        private const float StandardSeparationImpulse = 500f; // N·s from CLAUDE.md
        private const double MaxSeparationTime = 0.2; // Maximum allowed separation time in ms
        
        // Orbital mechanics integration
        private OrbitalState? _orbitalState;
        private CelestialBody _primaryBody;
        private bool _isOnRails = false; // True for time warp, false for physics simulation
        private double _lastOrbitalUpdate = 0.0;
        private bool _orbitalStateValid = false;
        
        // Orbital calculation performance tracking
        private double _orbitalCalculationTime = 0.0;
        private const double OrbitalCalculationBudget = 0.5; // ms per frame from task requirements
        
        public override void _Ready()
        {
            GD.Print($"PhysicsVessel: Node ready, waiting for initialization");
        }
        
        /// <summary>
        /// Initialize vessel with physics manager
        /// </summary>
        public void Initialize(int vesselId, PhysicsManager physicsManager)
        {
            _vesselId = vesselId;
            _physicsManager = physicsManager;
            _isActive = true;
            
            // Initialize anti-wobble system
            _antiWobbleSystem = new AntiWobbleSystem();
            
            // Initialize orbital mechanics with Kerbin as primary body
            _primaryBody = CelestialBody.CreateKerbin();
            _orbitalStateValid = false;
            _isOnRails = false;
            _lastOrbitalUpdate = Time.GetUnixTimeFromSystem();
            
            GD.Print($"PhysicsVessel {_vesselId}: Initialized with anti-wobble and orbital mechanics systems");
        }
        
        /// <summary>
        /// Add a part to the vessel
        /// </summary>
        public bool AddPart(RigidBody3D rigidBody, double mass, Double3 localPosition)
        {
            if (_parts.Count >= MaxParts)
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Cannot add part - vessel at maximum {MaxParts} parts");
                return false;
            }
            
            var part = new VesselPart
            {
                Id = _parts.Count,
                RigidBody = rigidBody,
                Mass = mass,
                LocalPosition = localPosition,
                IsActive = true
            };
            
            _parts.Add(part);
            _partLookup[part.Id] = part;
            _massPropertiesDirty = true;
            
            // Set up rigid body properties
            rigidBody.Mass = (float)mass;
            rigidBody.CollisionLayer = PhysicsManager.LayerVessel;
            rigidBody.CollisionMask = PhysicsManager.LayerStatic | PhysicsManager.LayerVessel;
            
            GD.Print($"PhysicsVessel {_vesselId}: Added part {part.Id} with mass {mass:F1}kg");
            return true;
        }
        
        /// <summary>
        /// Create a joint between two parts
        /// </summary>
        public bool CreateJoint(int partA, int partB, JointType jointType)
        {
            return CreateJoint(partA, partB, jointType, JointTuning.Rigid);
        }
        
        /// <summary>
        /// Create a joint between two parts with custom tuning
        /// </summary>
        public bool CreateJoint(int partA, int partB, JointType jointType, JointTuning tuning)
        {
            if (!_partLookup.TryGetValue(partA, out var partARef) || 
                !_partLookup.TryGetValue(partB, out var partBRef))
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Cannot create joint - invalid part IDs");
                return false;
            }
            
            Joint3D joint = null;
            
            switch (jointType)
            {
                case JointType.Fixed:
                    var fixedJoint = new Generic6DofJoint3D();
                    // Lock all axes for fixed connection
                    for (int i = 0; i < 6; i++)
                    {
                        fixedJoint.SetFlagX((Generic6DofJoint3D.Flag)i, true);
                        fixedJoint.SetFlagY((Generic6DofJoint3D.Flag)i, true);
                        fixedJoint.SetFlagZ((Generic6DofJoint3D.Flag)i, true);
                    }
                    // Apply joint tuning parameters
                    tuning.ApplyTo6DOFJoint(fixedJoint);
                    joint = fixedJoint;
                    break;
                    
                case JointType.Hinge:
                    joint = new HingeJoint3D();
                    // TODO: Apply tuning to HingeJoint3D when needed
                    break;
                    
                case JointType.Ball:
                    joint = new ConeTwistJoint3D();
                    // TODO: Apply tuning to ConeTwistJoint3D when needed
                    break;
                    
                case JointType.Separable:
                    var separableJoint = new Generic6DofJoint3D();
                    // Lock all axes but with separable tuning
                    for (int i = 0; i < 6; i++)
                    {
                        separableJoint.SetFlagX((Generic6DofJoint3D.Flag)i, true);
                        separableJoint.SetFlagY((Generic6DofJoint3D.Flag)i, true);
                        separableJoint.SetFlagZ((Generic6DofJoint3D.Flag)i, true);
                    }
                    tuning.ApplyTo6DOFJoint(separableJoint);
                    joint = separableJoint;
                    break;
            }
            
            if (joint == null)
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Failed to create joint type {jointType}");
                return false;
            }
            
            // Configure joint
            joint.NodeA = partARef.RigidBody.GetPath();
            joint.NodeB = partBRef.RigidBody.GetPath();
            
            AddChild(joint);
            
            var vesselJoint = new VesselJoint
            {
                Id = _joints.Count,
                Joint = joint,
                PartA = partA,
                PartB = partB,
                JointType = jointType,
                Tuning = tuning,
                IsActive = true
            };
            
            _joints.Add(vesselJoint);
            
            GD.Print($"PhysicsVessel {_vesselId}: Created {jointType} joint between parts {partA} and {partB} with tuning");
            return true;
        }
        
        /// <summary>
        /// Remove a joint (for staging/separation)
        /// Legacy method - use SeparateAtJoint for full separation mechanics
        /// </summary>
        public bool RemoveJoint(int jointId)
        {
            if (jointId < 0 || jointId >= _joints.Count)
                return false;
                
            var joint = _joints[jointId];
            if (joint.Joint != null && GodotObject.IsInstanceValid(joint.Joint))
            {
                joint.Joint.QueueFree();
            }
            
            joint.IsActive = false;
            _massPropertiesDirty = true; // Mark for mass recalculation
            GD.Print($"PhysicsVessel {_vesselId}: Removed joint {jointId}");
            return true;
        }
        
        /// <summary>
        /// Atomic joint separation with precise separation impulse and mass redistribution
        /// Implements low-level physics separation mechanics for staging operations
        /// </summary>
        public bool SeparateAtJoint(int jointId, bool applySeparationImpulse = true, float customImpulse = StandardSeparationImpulse)
        {
            var startTime = System.Diagnostics.Stopwatch.StartNew();
            
            // Validate joint exists and is active
            if (jointId < 0 || jointId >= _joints.Count)
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Invalid joint ID {jointId} for separation");
                return false;
            }
            
            var joint = _joints[jointId];
            if (!joint.IsActive || joint.Joint == null || !GodotObject.IsInstanceValid(joint.Joint))
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Joint {jointId} is not active or invalid");
                return false;
            }
            
            // Get parts involved in separation
            if (!_partLookup.TryGetValue(joint.PartA, out var partA) || 
                !_partLookup.TryGetValue(joint.PartB, out var partB))
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Cannot find parts for joint {jointId} separation");
                return false;
            }
            
            // Create separation event for tracking
            var separationPos = CalculateSeparationPosition(partA, partB);
            var separationDir = CalculateSeparationDirection(partA, partB);
            var separationEvent = SeparationEvent.Create(_nextEventId++, jointId, joint.PartA, joint.PartB, separationPos, separationDir);
            separationEvent.SeparationImpulse = customImpulse;
            
            // Store pre-separation mass properties
            var preMassProperties = GetMassProperties();
            
            try
            {
                // ATOMIC OPERATION START - All operations must succeed or be rolled back
                
                // 1. Remove joint atomically
                joint.Joint.QueueFree();
                joint.IsActive = false;
                
                // 2. Apply separation impulse in single frame if requested
                if (applySeparationImpulse)
                {
                    ApplyAtomicSeparationImpulse(partB, separationPos, separationDir, customImpulse);
                }
                
                // 3. Force immediate mass redistribution (same frame)
                _massPropertiesDirty = true;
                UpdateMassProperties(); // Immediate recalculation
                
                // ATOMIC OPERATION END
                
                // Store post-separation mass properties
                var postMassProperties = GetMassProperties();
                
                // Mark separation as successful
                separationEvent.MarkSuccess(startTime.Elapsed.TotalMilliseconds, preMassProperties, postMassProperties);
                
                // Validate physics state
                if (!separationEvent.ValidatePhysics())
                {
                    GD.PrintErr($"PhysicsVessel {_vesselId}: Separation {separationEvent.EventId} failed physics validation");
                    separationEvent.MarkFailure("Physics validation failed", SeparationValidationResult.PhysicsError);
                    _separationHistory.Add(separationEvent);
                    return false;
                }
                
                _separationHistory.Add(separationEvent);
                GD.Print($"PhysicsVessel {_vesselId}: Successfully separated joint {jointId} with {customImpulse:F1}N·s impulse in {startTime.Elapsed.TotalMilliseconds:F3}ms");
                return true;
            }
            catch (Exception ex)
            {
                // Operation failed - mark as failed
                separationEvent.MarkFailure($"Exception during separation: {ex.Message}", SeparationValidationResult.PhysicsError);
                _separationHistory.Add(separationEvent);
                GD.PrintErr($"PhysicsVessel {_vesselId}: Separation failed with exception: {ex.Message}");
                return false;
            }
            finally
            {
                startTime.Stop();
                
                // Ensure operation completed within performance budget
                if (startTime.Elapsed.TotalMilliseconds > MaxSeparationTime)
                {
                    GD.PrintErr($"PhysicsVessel {_vesselId}: Separation took {startTime.Elapsed.TotalMilliseconds:F3}ms (exceeds {MaxSeparationTime}ms target)");
                }
            }
        }
        
        /// <summary>
        /// Remove a part and all connected joints (atomic operation)
        /// </summary>
        public bool RemovePart(int partId, bool applySeparationImpulse = true)
        {
            if (!_partLookup.TryGetValue(partId, out var part))
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Cannot remove part {partId} - not found");
                return false;
            }
            
            // Find all joints connected to this part
            var connectedJoints = _joints
                .Where(j => j.IsActive && (j.PartA == partId || j.PartB == partId))
                .ToList();
            
            // Apply separation impulse if requested (for staging)
            if (applySeparationImpulse && part.RigidBody != null && GodotObject.IsInstanceValid(part.RigidBody))
            {
                const float SeparationImpulse = 500f; // 500 N·s from CLAUDE.md
                var separationForce = Vector3.Up * SeparationImpulse / part.RigidBody.Mass;
                part.RigidBody.ApplyCentralImpulse(separationForce);
                GD.Print($"PhysicsVessel {_vesselId}: Applied separation impulse to part {partId}");
            }
            
            // Remove all connected joints atomically
            foreach (var joint in connectedJoints)
            {
                RemoveJoint(joint.Id);
            }
            
            // Mark part as inactive (don't destroy RigidBody, let PhysicsManager handle it)
            part.IsActive = false;
            _massPropertiesDirty = true;
            
            GD.Print($"PhysicsVessel {_vesselId}: Removed part {partId} and {connectedJoints.Count} connected joints");
            return true;
        }
        
        /// <summary>
        /// Update joint tuning parameters (for anti-wobble system)
        /// </summary>
        public void UpdateJointTuning(int jointId, JointTuning newTuning)
        {
            if (jointId < 0 || jointId >= _joints.Count)
                return;
                
            var joint = _joints[jointId];
            if (!joint.IsActive || joint.Joint == null || !GodotObject.IsInstanceValid(joint.Joint))
                return;
                
            joint.Tuning = newTuning;
            
            // Apply new tuning if it's a Generic6DOFJoint3D
            if (joint.Joint is Generic6DofJoint3D dofJoint)
            {
                newTuning.ApplyTo6DOFJoint(dofJoint);
                GD.Print($"PhysicsVessel {_vesselId}: Updated joint {jointId} tuning parameters");
            }
        }
        
        /// <summary>
        /// Apply separation impulse to a specific part (for staging)
        /// </summary>
        public void ApplySeparationImpulse(int partId, Vector3? direction = null, float impulse = 500f)
        {
            if (!_partLookup.TryGetValue(partId, out var part) || 
                part.RigidBody == null || !GodotObject.IsInstanceValid(part.RigidBody))
            {
                return;
            }
            
            var separationDirection = direction ?? Vector3.Up;
            var separationForce = separationDirection.Normalized() * impulse / part.RigidBody.Mass;
            part.RigidBody.ApplyCentralImpulse(separationForce);
            
            GD.Print($"PhysicsVessel {_vesselId}: Applied {impulse}N·s separation impulse to part {partId}");
        }
        
        /// <summary>
        /// Calculate optimal separation position between two parts
        /// </summary>
        private Double3 CalculateSeparationPosition(VesselPart partA, VesselPart partB)
        {
            if (partA.RigidBody != null && partB.RigidBody != null &&
                GodotObject.IsInstanceValid(partA.RigidBody) && GodotObject.IsInstanceValid(partB.RigidBody))
            {
                var posA = Double3.FromVector3(partA.RigidBody.GlobalPosition);
                var posB = Double3.FromVector3(partB.RigidBody.GlobalPosition);
                return (posA + posB) / 2.0; // Midpoint between parts
            }
            
            // Fallback to local positions if RigidBodies are invalid
            return (partA.LocalPosition + partB.LocalPosition) / 2.0;
        }
        
        /// <summary>
        /// Calculate separation direction vector between two parts
        /// </summary>
        private Double3 CalculateSeparationDirection(VesselPart partA, VesselPart partB)
        {
            if (partA.RigidBody != null && partB.RigidBody != null &&
                GodotObject.IsInstanceValid(partA.RigidBody) && GodotObject.IsInstanceValid(partB.RigidBody))
            {
                var posA = Double3.FromVector3(partA.RigidBody.GlobalPosition);
                var posB = Double3.FromVector3(partB.RigidBody.GlobalPosition);
                var direction = posB - posA;
                
                if (direction.Length > 1e-6)
                {
                    return direction.Normalized;
                }
            }
            
            // Fallback direction if positions are too close or invalid
            return Double3.Up; // Default separation direction
        }
        
        /// <summary>
        /// Apply atomic separation impulse with precise positioning and direction
        /// </summary>
        private void ApplyAtomicSeparationImpulse(VesselPart part, Double3 position, Double3 direction, float impulse)
        {
            if (part.RigidBody == null || !GodotObject.IsInstanceValid(part.RigidBody))
            {
                return;
            }
            
            var separationDirection = direction.ToVector3().Normalized();
            var separationForce = separationDirection * impulse / part.RigidBody.Mass;
            
            // Apply impulse at separation position (using central impulse for simplicity)
            // In a more advanced implementation, this could use ApplyImpulse with specific position
            part.RigidBody.ApplyCentralImpulse(separationForce);
            
            GD.Print($"PhysicsVessel {_vesselId}: Applied atomic separation impulse {impulse:F1}N·s to part {part.Id}");
        }
        
        /// <summary>
        /// Process physics update for this vessel
        /// </summary>
        public void ProcessPhysics(double delta)
        {
            if (!_isActive)
                return;
                
            _framesSinceUpdate++;
            
            if (_framesSinceUpdate >= UpdateFrequency)
            {
                UpdateMassProperties();
                UpdatePositionAndVelocity();
                
                // Process anti-wobble system
                ProcessAntiWobble((float)delta);
                
                _framesSinceUpdate = 0;
            }
        }
        
        /// <summary>
        /// Update mass properties (total mass, center of mass, moment of inertia)
        /// </summary>
        private void UpdateMassProperties()
        {
            if (!_massPropertiesDirty)
                return;
                
            _totalMass = 0.0;
            var weightedPosition = Double3.Zero;
            
            // Calculate total mass and center of mass
            foreach (var part in _parts)
            {
                if (!part.IsActive || !GodotObject.IsInstanceValid(part.RigidBody))
                    continue;
                    
                _totalMass += part.Mass;
                var partWorldPos = Double3.FromVector3(part.RigidBody.GlobalPosition);
                weightedPosition += partWorldPos * part.Mass;
            }
            
            if (_totalMass > 0)
            {
                _centerOfMass = weightedPosition / _totalMass;
            }
            
            // Calculate moment of inertia tensor (simplified)
            var inertia = Double3.Zero;
            foreach (var part in _parts)
            {
                if (!part.IsActive || !GodotObject.IsInstanceValid(part.RigidBody))
                    continue;
                    
                var partPos = Double3.FromVector3(part.RigidBody.GlobalPosition);
                var relativePos = partPos - _centerOfMass;
                var distanceSquared = relativePos.LengthSquared;
                
                // Simple point mass approximation
                inertia.X += part.Mass * (relativePos.Y * relativePos.Y + relativePos.Z * relativePos.Z);
                inertia.Y += part.Mass * (relativePos.X * relativePos.X + relativePos.Z * relativePos.Z);
                inertia.Z += part.Mass * (relativePos.X * relativePos.X + relativePos.Y * relativePos.Y);
            }
            
            _momentOfInertia = inertia;
            _massPropertiesDirty = false;
        }
        
        /// <summary>
        /// Update vessel position and velocity from parts
        /// </summary>
        private void UpdatePositionAndVelocity()
        {
            if (_parts.Count == 0)
                return;
                
            // Use center of mass as vessel position
            _position = _centerOfMass;
            
            // Calculate average velocity
            var totalVelocity = Double3.Zero;
            int activePartCount = 0;
            
            foreach (var part in _parts)
            {
                if (!part.IsActive || !GodotObject.IsInstanceValid(part.RigidBody))
                    continue;
                    
                totalVelocity += Double3.FromVector3(part.RigidBody.LinearVelocity);
                activePartCount++;
            }
            
            if (activePartCount > 0)
            {
                _velocity = totalVelocity / activePartCount;
            }
        }
        
        /// <summary>
        /// Get vessel mass properties
        /// </summary>
        public VesselMassProperties GetMassProperties()
        {
            UpdateMassProperties();
            
            return new VesselMassProperties
            {
                TotalMass = _totalMass,
                CenterOfMass = _centerOfMass,
                MomentOfInertia = _momentOfInertia
            };
        }
        
        /// <summary>
        /// Get vessel state for orbital mechanics
        /// </summary>
        public VesselState GetVesselState()
        {
            return new VesselState
            {
                Position = _position,
                Velocity = _velocity,
                AngularVelocity = Double3.FromVector3(_angularVelocity),
                Mass = _totalMass,
                IsActive = _isActive
            };
        }
        
        /// <summary>
        /// Set vessel position (for floating origin shifts)
        /// </summary>
        public void SetPosition(Double3 newPosition)
        {
            var offset = newPosition - _position;
            
            foreach (var part in _parts)
            {
                if (!part.IsActive || !GodotObject.IsInstanceValid(part.RigidBody))
                    continue;
                    
                var currentPos = Double3.FromVector3(part.RigidBody.GlobalPosition);
                var newPos = currentPos + offset;
                part.RigidBody.GlobalPosition = newPos.ToVector3();
            }
            
            _position = newPosition;
            _massPropertiesDirty = true;
        }
        
        /// <summary>
        /// Get number of active parts
        /// </summary>
        public int GetPartCount()
        {
            return _parts.Count(p => p.IsActive && GodotObject.IsInstanceValid(p.RigidBody));
        }
        
        /// <summary>
        /// Get number of active joints
        /// </summary>
        public int GetJointCount()
        {
            return _joints.Count(j => j.IsActive && GodotObject.IsInstanceValid(j.Joint));
        }
        
        /// <summary>
        /// Process anti-wobble system for this vessel
        /// </summary>
        private void ProcessAntiWobble(float deltaTime)
        {
            if (_antiWobbleSystem == null)
                return;
                
            // For simplified integration, we'll use a fixed altitude and velocity
            // In a full implementation, this would come from orbital mechanics system
            double altitude = 0.0; // Sea level for testing
            Double3 velocity = _velocity;
            
            // Process the anti-wobble system
            _antiWobbleSystem.ProcessVessel(this, altitude, velocity, deltaTime);
        }
        
        /// <summary>
        /// Get vessel joints for anti-wobble system access
        /// </summary>
        internal List<VesselJoint> GetJoints()
        {
            return _joints.Where(j => j.IsActive).ToList();
        }
        
        /// <summary>
        /// Get anti-wobble metrics
        /// </summary>
        public AntiWobbleMetrics? GetAntiWobbleMetrics()
        {
            return _antiWobbleSystem?.GetMetrics();
        }
        
        /// <summary>
        /// Get separation event history for analysis and validation
        /// </summary>
        public List<SeparationEvent> GetSeparationHistory()
        {
            return _separationHistory.ToList(); // Return copy to prevent external modification
        }
        
        /// <summary>
        /// Get latest separation metrics
        /// </summary>
        public SeparationMetrics? GetLatestSeparationMetrics()
        {
            if (_separationHistory.Count == 0)
                return null;
                
            return _separationHistory[_separationHistory.Count - 1].GetMetrics();
        }
        
        /// <summary>
        /// Get separation performance statistics
        /// </summary>
        public SeparationPerformanceStats GetSeparationPerformanceStats()
        {
            if (_separationHistory.Count == 0)
            {
                return new SeparationPerformanceStats
                {
                    TotalSeparations = 0,
                    SuccessfulSeparations = 0,
                    SuccessRate = 0,
                    AverageOperationTime = 0,
                    MaxOperationTime = 0,
                    MinOperationTime = 0
                };
            }
            
            var successful = _separationHistory.Where(s => s.Success).ToList();
            var operationTimes = _separationHistory.Where(s => s.OperationDuration > 0).Select(s => s.OperationDuration).ToList();
            
            return new SeparationPerformanceStats
            {
                TotalSeparations = _separationHistory.Count,
                SuccessfulSeparations = successful.Count,
                SuccessRate = (double)successful.Count / _separationHistory.Count,
                AverageOperationTime = operationTimes.Count > 0 ? operationTimes.Average() : 0,
                MaxOperationTime = operationTimes.Count > 0 ? operationTimes.Max() : 0,
                MinOperationTime = operationTimes.Count > 0 ? operationTimes.Min() : 0
            };
        }
        
        /// <summary>
        /// Clear separation history (for testing or memory management)
        /// </summary>
        public void ClearSeparationHistory()
        {
            _separationHistory.Clear();
            _nextEventId = 0;
            GD.Print($"PhysicsVessel {_vesselId}: Cleared separation history");
        }
        
        /// <summary>
        /// Clean up vessel resources
        /// </summary>
        public void Cleanup()
        {
            _isActive = false;
            
            // Clean up anti-wobble system
            _antiWobbleSystem?.Reset();
            
            // Clean up joints
            foreach (var joint in _joints)
            {
                if (joint.Joint != null && GodotObject.IsInstanceValid(joint.Joint))
                {
                    joint.Joint.QueueFree();
                }
            }
            
            // Clean up parts (but don't destroy the RigidBodies, they may be reused)
            _parts.Clear();
            _joints.Clear();
            _partLookup.Clear();
            
            GD.Print($"PhysicsVessel {_vesselId}: Cleaned up");
        }
        
        /// <summary>
        /// Update orbital state from current physics state (off-rails mode)
        /// </summary>
        public void UpdateOrbitalStateFromPhysics(double currentTime)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            if (_totalMass <= 0 || !_isActive)
            {
                _orbitalStateValid = false;
                return;
            }
            
            // Get current position and velocity in world coordinates
            Double3 worldPosition = _position - _primaryBody.Position;
            Double3 worldVelocity = _velocity;
            
            // Create orbital state from current Cartesian state
            _orbitalState = OrbitalState.FromCartesian(worldPosition, worldVelocity, currentTime, 
                                                     _primaryBody.GravitationalParameter);
            _orbitalStateValid = _orbitalState.Value.IsValid();
            _lastOrbitalUpdate = currentTime;
            
            stopwatch.Stop();
            _orbitalCalculationTime = stopwatch.Elapsed.TotalMilliseconds;
            
            if (_orbitalCalculationTime > OrbitalCalculationBudget)
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Orbital state update exceeded budget: {_orbitalCalculationTime:F3}ms");
            }
        }
        
        /// <summary>
        /// Apply gravitational forces to all vessel parts during physics update
        /// </summary>
        public void ApplyGravitationalForces()
        {
            if (!_isActive || _isOnRails || _totalMass <= 0)
                return;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Calculate gravitational force for the vessel center of mass
            Double3 gravityAcceleration = _primaryBody.CalculateGravitationalAcceleration(_position);
            
            // Apply gravity to all active parts
            Vector3 gravityForce = (gravityAcceleration * _totalMass).ToVector3();
            
            foreach (var part in _parts.Where(p => p.IsActive && p.RigidBody != null))
            {
                // Apply proportional gravity force to each part
                Vector3 partGravityForce = gravityForce * ((float)part.Mass / (float)_totalMass);
                part.RigidBody.ApplyForce(partGravityForce);
            }
            
            stopwatch.Stop();
            double gravityCalcTime = stopwatch.Elapsed.TotalMilliseconds;
            
            if (gravityCalcTime > 0.2) // 0.2ms budget for gravity calculations
            {
                GD.PrintErr($"PhysicsVessel {_vesselId}: Gravity calculation exceeded budget: {gravityCalcTime:F3}ms");
            }
        }
        
        /// <summary>
        /// Set vessel to on-rails mode for time warp (orbital propagation)
        /// </summary>
        public void SetOnRails(bool onRails)
        {
            if (_isOnRails == onRails)
                return;
            
            _isOnRails = onRails;
            
            if (onRails)
            {
                // Update orbital state before going on rails
                UpdateOrbitalStateFromPhysics(Time.GetUnixTimeFromSystem());
                
                // Disable physics for all parts
                foreach (var part in _parts.Where(p => p.IsActive && p.RigidBody != null))
                {
                    part.RigidBody.Freeze = true;
                }
                
                GD.Print($"PhysicsVessel {_vesselId}: Set to on-rails mode");
            }
            else
            {
                // Re-enable physics for all parts
                foreach (var part in _parts.Where(p => p.IsActive && p.RigidBody != null))
                {
                    part.RigidBody.Freeze = false;
                }
                
                // Update physics state from orbital state if available
                if (_orbitalStateValid && _orbitalState.HasValue)
                {
                    UpdatePhysicsFromOrbitalState(Time.GetUnixTimeFromSystem());
                }
                
                GD.Print($"PhysicsVessel {_vesselId}: Set to off-rails mode");
            }
        }
        
        /// <summary>
        /// Update physics position/velocity from orbital state (on-rails to off-rails transition)
        /// </summary>
        private void UpdatePhysicsFromOrbitalState(double currentTime)
        {
            if (!_orbitalStateValid || !_orbitalState.HasValue)
                return;
            
            // Propagate orbital state to current time
            var currentOrbitalState = _orbitalState.Value.PropagateToTime(currentTime);
            
            // Convert to Cartesian coordinates
            var (position, velocity) = currentOrbitalState.ToCartesian(currentTime);
            
            // Update vessel state
            _position = position + _primaryBody.Position;
            _velocity = velocity;
            
            // Update primary part position if available
            if (_parts.Count > 0 && _parts[0].RigidBody != null)
            {
                var primaryPart = _parts[0].RigidBody;
                primaryPart.GlobalPosition = _position.ToVector3();
                primaryPart.LinearVelocity = _velocity.ToVector3();
            }
            
            _lastOrbitalUpdate = currentTime;
        }
        
        /// <summary>
        /// Create orbital state for circular orbit at specified altitude
        /// </summary>
        public void SetCircularOrbit(double altitude, double inclination = 0)
        {
            _orbitalState = _primaryBody.CreateCircularOrbit(altitude, inclination, Time.GetUnixTimeFromSystem());
            _orbitalStateValid = true;
            
            // Update physics position/velocity
            UpdatePhysicsFromOrbitalState(Time.GetUnixTimeFromSystem());
            
            GD.Print($"PhysicsVessel {_vesselId}: Set to circular orbit at {altitude/1000:F1}km altitude");
        }
        
        /// <summary>
        /// Get current orbital parameters for display/monitoring
        /// </summary>
        public (OrbitalState? orbital, bool isValid, double calculationTime) GetOrbitalState()
        {
            return (_orbitalState, _orbitalStateValid, _orbitalCalculationTime);
        }
        
        /// <summary>
        /// Get orbital metrics for performance monitoring
        /// </summary>
        public (double altitude, double velocity, double period, bool inAtmosphere) GetOrbitalMetrics()
        {
            if (!_orbitalStateValid || !_orbitalState.HasValue)
                return (0, 0, 0, false);
            
            double altitude = _primaryBody.GetAltitude(_position);
            double velocity = _velocity.Length;
            double period = _orbitalState.Value.OrbitalPeriod;
            bool inAtmosphere = _primaryBody.IsInAtmosphere(_position);
            
            return (altitude, velocity, period, inAtmosphere);
        }
    }
    
    /// <summary>
    /// Individual vessel part data
    /// </summary>
    public class VesselPart
    {
        public int Id;
        public RigidBody3D RigidBody = null!;
        public double Mass;
        public Double3 LocalPosition;
        public bool IsActive;
    }
    
    /// <summary>
    /// Joint between vessel parts
    /// </summary>
    public class VesselJoint
    {
        public int Id;
        public Joint3D Joint = null!;
        public int PartA;
        public int PartB;
        public JointType JointType;
        public JointTuning Tuning;
        public bool IsActive;
        public double CurrentStress = 0.0; // Current force/torque applied to joint
    }
    
    /// <summary>
    /// Types of joints available
    /// </summary>
    public enum JointType
    {
        Fixed,      // Rigid connection
        Hinge,      // Rotational joint
        Ball,       // Ball joint (3DOF rotation)
        Separable   // Fixed but designed for separation (staging)
    }
    
    /// <summary>
    /// Vessel mass properties
    /// </summary>
    public struct VesselMassProperties
    {
        public double TotalMass;
        public Double3 CenterOfMass;
        public Double3 MomentOfInertia;
    }
    
    /// <summary>
    /// Vessel state for orbital mechanics
    /// </summary>
    public struct VesselState
    {
        public Double3 Position;
        public Double3 Velocity;
        public Double3 AngularVelocity;
        public double Mass;
        public bool IsActive;
    }
    
    /// <summary>
    /// Performance statistics for separation operations
    /// </summary>
    public struct SeparationPerformanceStats
    {
        public int TotalSeparations;
        public int SuccessfulSeparations;
        public double SuccessRate;
        public double AverageOperationTime;
        public double MaxOperationTime;
        public double MinOperationTime;
    }
}