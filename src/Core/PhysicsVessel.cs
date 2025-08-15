using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Lithobrake.Core.Utils;

namespace Lithobrake.Core
{
    /// <summary>
    /// Multi-part vessel physics handler for rocket simulation.
    /// Manages parts list, joints, mass properties, and vessel lifecycle.
    /// Integrates with Double3 coordinate system for orbital mechanics.
    /// Supports floating origin system for precision preservation.
    /// </summary>
    public partial class PhysicsVessel : Node3D, IOriginShiftAware, IDisposable
    {
        // Vessel identification
        private int _vesselId;
        private PhysicsManager? _physicsManager;
        
        // Parts and joints management
        private readonly List<VesselPart> _parts = new();
        private readonly List<VesselJoint> _joints = new();
        private readonly Dictionary<int, VesselPart> _partLookup = new();
        private readonly Dictionary<int, VesselJoint> _jointLookup = new();
        
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
        private const int MaxParts = UNIVERSE_CONSTANTS.MAX_PARTS_PER_VESSEL;
        
        // Anti-wobble system
        private AntiWobbleSystem? _antiWobbleSystem;
        
        // Separation event system
        private readonly List<SeparationEvent> _separationHistory = new();
        private int _nextEventId = 0;
        
        // Performance constants for separation system
        private const float StandardSeparationImpulse = 500f; // N·s from CLAUDE.md
        private const double MaxSeparationTime = 0.2; // Maximum allowed separation time in ms
        
        // Public properties for atmospheric system integration
        public IEnumerable<Part> Parts => _parts
            .Where(vp => vp.PartReference != null && vp.IsActive)
            .Select(vp => vp.PartReference!);
            
        /// <summary>
        /// Get the root part of this vessel (first active part with a reference)
        /// </summary>
        public Part? RootPart => _parts.FirstOrDefault(vp => vp.PartReference != null)?.PartReference;
        
        // Orbital mechanics integration
        private OrbitalState? _orbitalState;
        private CelestialBody _primaryBody;
        private bool _isOnRails = false; // True for time warp, false for physics simulation
        private double _lastOrbitalUpdate = 0.0;
        private bool _orbitalStateValid = false;
        
        // Orbital calculation performance tracking
        private double _orbitalCalculationTime = 0.0;
        private const double OrbitalCalculationBudget = 0.5; // ms per frame from task requirements
        
        // Floating origin system integration
        private bool _isOriginShiftRegistered = false;
        
        // Thrust system integration
        private readonly List<Engine> _engines = new();
        private readonly List<FuelTank> _fuelTanks = new();
        private double _currentThrottle = 0.0;
        
        // Cached component references to avoid O(n²) searches
        private readonly Dictionary<int, List<Engine>> _partEngines = new();
        private readonly Dictionary<int, List<FuelTank>> _partFuelTanks = new();
        private double _lastThrustUpdate = 0.0;
        private const double ThrustUpdateFrequency = 1.0 / 60.0; // 60Hz thrust updates
        
        /// <summary>
        /// Godot lifecycle method called when node is ready
        /// </summary>
        public override void _Ready()
        {
            Debug.Log($"PhysicsVessel: Node ready, waiting for initialization");
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
            
            // Register with floating origin system
            FloatingOriginManager.RegisterOriginShiftAware(this);
            
            Debug.Log($"PhysicsVessel {_vesselId}: Initialized with anti-wobble, orbital mechanics, and floating origin systems");
        }
        
        /// <summary>
        /// Add a part to the vessel
        /// </summary>
        public bool AddPart(RigidBody3D rigidBody, double mass, Double3 localPosition)
        {
            if (_parts.Count >= MaxParts)
            {
                Debug.LogError($"PhysicsVessel {_vesselId}: Cannot add part - vessel at maximum {MaxParts} parts");
                return false;
            }
            
            var part = new VesselPart
            {
                Id = _parts.Count,
                Mass = mass,
                LocalPosition = localPosition,
                IsActive = true
            };
            part.SetRigidBody(rigidBody);
            
            _parts.Add(part);
            _partLookup[part.Id] = part;
            _massPropertiesDirty = true;
            
            // Set up rigid body properties
            rigidBody.Mass = (float)mass;
            rigidBody.CollisionLayer = PhysicsManager.LayerVessel;
            rigidBody.CollisionMask = PhysicsManager.LayerStatic | PhysicsManager.LayerVessel;
            
            // Cache component references to avoid O(n²) searches later
            CachePartComponents(part);
            
            // Invalidate FuelFlowSystem cache when parts change
            FuelFlowSystem.InvalidateCache();
            
            Debug.Log($"PhysicsVessel {_vesselId}: Added part {part.Id} with mass {mass:F1}kg");
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
                Debug.LogError($"PhysicsVessel {_vesselId}: Cannot create joint - invalid part IDs");
                return false;
            }
            
            // Validate RigidBody objects are usable
            if (!partARef.IsUsable || !partBRef.IsUsable)
            {
                DebugLog.LogError($"PhysicsVessel {_vesselId}: Cannot create joint - parts not usable");
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
                    // NOTE: HingeJoint3D tuning limited by Godot 4.4.1 API
                    // Only basic parameters exposed, no advanced stiffness/damping control
                    break;
                    
                case JointType.Ball:
                    joint = new ConeTwistJoint3D();
                    // NOTE: ConeTwistJoint3D tuning limited by Godot 4.4.1 API
                    // Only basic parameters exposed, no advanced stiffness/damping control
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
                Debug.LogError($"PhysicsVessel {_vesselId}: Failed to create joint type {jointType}");
                return false;
            }
            
            // Configure joint using safe access
            var nodeAPath = partARef.TryUseRigidBody<NodePath?>(rb => rb.GetPath());
            var nodeBPath = partBRef.TryUseRigidBody<NodePath?>(rb => rb.GetPath());
            
            if (nodeAPath == null || nodeBPath == null)
            {
                DebugLog.LogError($"PhysicsVessel {_vesselId}: Failed to get node paths for joint creation");
                return false;
            }
            
            joint.NodeA = nodeAPath;
            joint.NodeB = nodeBPath;
            
            AddChild(joint);
            
            var vesselJoint = new VesselJoint
            {
                Id = _joints.Count,
                PartA = partA,
                PartB = partB,
                JointType = jointType,
                Tuning = tuning,
                IsActive = true
            };
            vesselJoint.SetJoint(joint);
            
            _joints.Add(vesselJoint);
            
            Debug.Log($"PhysicsVessel {_vesselId}: Created {jointType} joint between parts {partA} and {partB} with tuning");
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
            
            // Use safe disposal
            SafeOperations.SafeDispose(joint, $"VesselJoint[{jointId}]");
            
            _massPropertiesDirty = true; // Mark for mass recalculation
            DebugLog.Log($"PhysicsVessel {_vesselId}: Removed joint {jointId}");
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
                Debug.LogError($"PhysicsVessel {_vesselId}: Invalid joint ID {jointId} for separation");
                return false;
            }
            
            var joint = _joints[jointId];
            if (!joint.IsActive || !joint.IsUsable)
            {
                DebugLog.LogError($"PhysicsVessel {_vesselId}: Joint {jointId} is not active or usable");
                return false;
            }
            
            // Get parts involved in separation
            if (!_partLookup.TryGetValue(joint.PartA, out var partA) || 
                !_partLookup.TryGetValue(joint.PartB, out var partB))
            {
                Debug.LogError($"PhysicsVessel {_vesselId}: Cannot find parts for joint {jointId} separation");
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
                
                // 1. Remove joint atomically using safe disposal
                SafeOperations.SafeDispose(joint, $"VesselJoint[{jointId}]");
                
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
                    Debug.LogError($"PhysicsVessel {_vesselId}: Separation {separationEvent.EventId} failed physics validation");
                    separationEvent.MarkFailure("Physics validation failed", SeparationValidationResult.PhysicsError);
                    _separationHistory.Add(separationEvent);
                    return false;
                }
                
                _separationHistory.Add(separationEvent);
                Debug.Log($"PhysicsVessel {_vesselId}: Successfully separated joint {jointId} with {customImpulse:F1}N·s impulse in {startTime.Elapsed.TotalMilliseconds:F3}ms");
                return true;
            }
            catch (Exception ex)
            {
                // Operation failed - mark as failed
                separationEvent.MarkFailure($"Exception during separation: {ex.Message}", SeparationValidationResult.PhysicsError);
                _separationHistory.Add(separationEvent);
                Debug.LogError($"PhysicsVessel {_vesselId}: Separation failed with exception: {ex.Message}");
                return false;
            }
            finally
            {
                startTime.Stop();
                
                // Ensure operation completed within performance budget
                if (startTime.Elapsed.TotalMilliseconds > MaxSeparationTime)
                {
                    Debug.LogWarning($"PhysicsVessel {_vesselId}: Separation took {startTime.Elapsed.TotalMilliseconds:F3}ms (exceeds {MaxSeparationTime}ms target)");
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
                Debug.LogError($"PhysicsVessel {_vesselId}: Cannot remove part {partId} - not found");
                return false;
            }
            
            // Find all joints connected to this part
            var connectedJoints = _joints
                .Where(j => j.IsActive && (j.PartA == partId || j.PartB == partId))
                .ToList();
            
            // Apply separation impulse if requested (for staging)
            if (applySeparationImpulse && part.IsUsable)
            {
                const float SeparationImpulse = 500f; // 500 N·s from CLAUDE.md
                part.TryUseRigidBody(rigidBody =>
                {
                    var separationForce = Vector3.Up * SeparationImpulse / rigidBody.Mass;
                    rigidBody.ApplyCentralImpulse(separationForce);
                    DebugLog.Log($"PhysicsVessel {_vesselId}: Applied separation impulse to part {partId}");
                });
            }
            
            // Remove all connected joints atomically
            foreach (var joint in connectedJoints)
            {
                RemoveJoint(joint.Id);
            }
            
            // Mark part as inactive (don't destroy RigidBody, let PhysicsManager handle it)
            part.IsActive = false;
            _massPropertiesDirty = true;
            
            // Clear cached component references for this part
            ClearPartComponents(partId);
            
            // Invalidate FuelFlowSystem cache when parts change
            FuelFlowSystem.InvalidateCache();
            
            Debug.Log($"PhysicsVessel {_vesselId}: Removed part {partId} and {connectedJoints.Count} connected joints");
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
            if (!joint.IsActive || !joint.IsUsable)
                return;
                
            joint.Tuning = newTuning;
            
            // Apply new tuning if it's a Generic6DOFJoint3D
            joint.TryUseJoint(jointObj =>
            {
                if (jointObj is Generic6DofJoint3D dofJoint)
                {
                    newTuning.ApplyTo6DOFJoint(dofJoint);
                    DebugLog.Log($"PhysicsVessel {_vesselId}: Updated joint {jointId} tuning parameters");
                }
            });
        }
        
        /// <summary>
        /// Apply separation impulse to a specific part (for staging)
        /// </summary>
        public void ApplySeparationImpulse(int partId, Vector3? direction = null, float impulse = 500f)
        {
            if (!_partLookup.TryGetValue(partId, out var part) || !part.IsUsable)
            {
                return;
            }
            
            var separationDirection = direction ?? Vector3.Up;
            part.TryUseRigidBody(rigidBody =>
            {
                var separationForce = separationDirection.Normalized() * impulse / rigidBody.Mass;
                rigidBody.ApplyCentralImpulse(separationForce);
                DebugLog.Log($"PhysicsVessel {_vesselId}: Applied {impulse}N·s separation impulse to part {partId}");
            });
        }
        
        /// <summary>
        /// Get the world position of a part, with fallback to local position
        /// </summary>
        private Double3 GetPartWorldPosition(VesselPart part)
        {
            return part.TryUseRigidBodyWithDefaultValue(
                rb => Double3.FromVector3(rb.GlobalPosition), 
                part.LocalPosition);
        }
        
        /// <summary>
        /// Calculate optimal separation position between two parts
        /// </summary>
        private Double3 CalculateSeparationPosition(VesselPart partA, VesselPart partB)
        {
            if (partA.IsUsable && partB.IsUsable)
            {
                var posA = GetPartWorldPosition(partA);
                var posB = GetPartWorldPosition(partB);
                return (posA + posB) / 2.0; // Midpoint between parts
            }
            
            // Fallback to local positions if RigidBodies are not usable
            return (partA.LocalPosition + partB.LocalPosition) / 2.0;
        }
        
        /// <summary>
        /// Calculate separation direction vector between two parts
        /// </summary>
        private Double3 CalculateSeparationDirection(VesselPart partA, VesselPart partB)
        {
            if (partA.IsUsable && partB.IsUsable)
            {
                var posA = GetPartWorldPosition(partA);
                var posB = GetPartWorldPosition(partB);
                var direction = posB - posA;
                
                if (direction.Length > 1e-6)
                {
                    return direction.Normalized;
                }
            }
            
            // Fallback direction if positions are too close or parts not usable
            return Double3.Up; // Default separation direction
        }
        
        /// <summary>
        /// Apply atomic separation impulse with precise positioning and direction
        /// </summary>
        private void ApplyAtomicSeparationImpulse(VesselPart part, Double3 position, Double3 direction, float impulse)
        {
            if (!part.IsUsable)
            {
                return;
            }
            
            var separationDirection = direction.ToVector3().Normalized();
            
            part.TryUseRigidBody(rigidBody =>
            {
                var separationForce = separationDirection * impulse / rigidBody.Mass;
                
                // Apply impulse at separation position (using central impulse for simplicity)
                // In a more advanced implementation, this could use ApplyImpulse with specific position
                rigidBody.ApplyCentralImpulse(separationForce);
                
                DebugLog.Log($"PhysicsVessel {_vesselId}: Applied atomic separation impulse {impulse:F1}N·s to part {part.Id}");
            });
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
                
                // Process atmospheric forces (drag, heating, dynamic pressure)
                ProcessAtmosphericForces(delta);
                
                // Process thrust and fuel systems
                ProcessThrustAndFuel(delta);
                
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
                if (!part.IsActive || !part.IsUsable)
                    continue;
                    
                _totalMass += part.Mass;
                
                // Use safe access to get global position
                var partWorldPos = GetPartWorldPosition(part);
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
                if (!part.IsActive || !part.IsUsable)
                    continue;
                    
                var partPos = GetPartWorldPosition(part);
                var relativePos = partPos - _centerOfMass;
                
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
                if (!part.IsActive || !part.IsUsable)
                    continue;
                    
                var velocity = part.TryUseRigidBodyWithDefaultValue(
                    rb => Double3.FromVector3(rb.LinearVelocity), 
                    Double3.Zero);
                totalVelocity += velocity;
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
                if (!part.IsActive || !part.IsUsable)
                    continue;
                    
                part.TryUseRigidBody(rigidBody =>
                {
                    var currentPos = Double3.FromVector3(rigidBody.GlobalPosition);
                    var newPos = currentPos + offset;
                    rigidBody.GlobalPosition = newPos.ToVector3();
                });
            }
            
            _position = newPosition;
            _massPropertiesDirty = true;
        }
        
        /// <summary>
        /// Get number of active parts
        /// </summary>
        public int GetPartCount()
        {
            return _parts.Count(p => p.IsActive && p.IsUsable);
        }
        
        /// <summary>
        /// Get number of active joints
        /// </summary>
        public int GetJointCount()
        {
            return _joints.Count(j => j.IsActive && j.IsUsable);
        }
        
        /// <summary>
        /// Process thrust and fuel systems for this vessel
        /// </summary>
        private void ProcessThrustAndFuel(double delta)
        {
            var currentTime = Time.GetUnixTimeFromSystem();
            
            // Throttle control updates
            if (ThrottleController.Instance != null)
            {
                _currentThrottle = ThrottleController.Instance.GetCurrentThrottle();
            }
            
            // Update engines and fuel tanks if time for thrust update
            if (currentTime - _lastThrustUpdate >= ThrustUpdateFrequency)
            {
                UpdateEnginesAndFuelTanks();
                
                // Calculate and apply thrust forces
                if (_engines.Count > 0)
                {
                    ProcessThrustForces(delta);
                }
                
                // Process fuel flow and consumption
                if (_engines.Count > 0 && _fuelTanks.Count > 0)
                {
                    ProcessFuelFlow(delta);
                }
                
                // Update visual effects
                UpdateEngineEffects();
                
                _lastThrustUpdate = currentTime;
            }
        }
        
        /// <summary>
        /// Process thrust forces for all engines
        /// </summary>
        private void ProcessThrustForces(double delta)
        {
            // Get atmospheric pressure for thrust efficiency calculations
            var altitude = GlobalPosition.Y;
            var atmosphericPressure = GetAtmosphericPressure(altitude);
            
            // Calculate thrust for all engines
            var thrustResult = ThrustSystem.CalculateVesselThrust(_engines, _currentThrottle, atmosphericPressure);
            
            // Apply thrust forces to physics bodies
            ThrustSystem.ApplyThrustForces(this, thrustResult);
        }
        
        /// <summary>
        /// Process fuel flow and consumption
        /// </summary>
        private void ProcessFuelFlow(double delta)
        {
            // Update fuel flow between tanks and engines
            var fuelResult = FuelFlowSystem.UpdateFuelFlow(_engines, _fuelTanks, delta);
            
            // Handle fuel starvation events
            foreach (var starvationEvent in fuelResult.StarvationEvents)
            {
                Debug.LogError($"PhysicsVessel {_vesselId}: Engine {starvationEvent.Engine.PartName} starved of fuel");
                starvationEvent.Engine.SetActive(false);
            }
            
            // Update mass properties if fuel consumed
            if (fuelResult.TotalFuelConsumed > 0.1) // Threshold to avoid excessive updates
            {
                _massPropertiesDirty = true;
            }
        }
        
        /// <summary>
        /// Update engine visual effects
        /// </summary>
        private void UpdateEngineEffects()
        {
            if (EffectsManager.Instance == null)
                return;
            
            var altitude = GlobalPosition.Y;
            var atmosphericPressure = GetAtmosphericPressure(altitude);
            
            foreach (var engine in _engines)
            {
                // Update exhaust effects
                EffectsManager.Instance.UpdateEngineExhaust(engine, _currentThrottle, atmosphericPressure);
                
                // Update thrust visualization
                var thrust = engine.GetThrust(_currentThrottle, atmosphericPressure);
                var efficiency = thrust / engine.MaxThrust; // Simple efficiency calculation
                EffectsManager.Instance.UpdateThrustVisualization(engine, thrust, efficiency);
            }
        }
        
        /// <summary>
        /// Cache component references for a part to avoid O(n²) searches
        /// </summary>
        private void CachePartComponents(VesselPart part)
        {
            if (!part.IsUsable)
                return;
                
            var engines = new List<Engine>();
            var fuelTanks = new List<FuelTank>();
            
            part.TryUseRigidBody(rigidBody =>
            {
                foreach (Node child in rigidBody.GetChildren())
                {
                    if (child is Engine engine && !engine.IsQueuedForDeletion())
                    {
                        engines.Add(engine);
                    }
                    else if (child is FuelTank fuelTank && !fuelTank.IsQueuedForDeletion())
                    {
                        fuelTanks.Add(fuelTank);
                    }
                }
            });
            
            _partEngines[part.Id] = engines;
            _partFuelTanks[part.Id] = fuelTanks;
        }
        
        /// <summary>
        /// Clear cached component references for a part
        /// </summary>
        private void ClearPartComponents(int partId)
        {
            _partEngines.Remove(partId);
            _partFuelTanks.Remove(partId);
        }
        
        /// <summary>
        /// Update lists of engines and fuel tanks from vessel parts
        /// </summary>
        private void UpdateEnginesAndFuelTanks()
        {
            // Update engines list
            _engines.Clear();
            _fuelTanks.Clear();
            
            // Use cached component references to avoid O(n²) searches
            foreach (var part in _parts)
            {
                if (!part.IsActive)
                    continue;
                
                // Add cached engines for this part
                if (_partEngines.TryGetValue(part.Id, out var engines))
                {
                    foreach (var engine in engines)
                    {
                        if (!engine.IsQueuedForDeletion())
                        {
                            _engines.Add(engine);
                            
                            // Register engine with throttle controller
                            if (ThrottleController.Instance != null)
                            {
                                ThrottleController.Instance.RegisterEngine(engine);
                            }
                        }
                    }
                }
                
                // Add cached fuel tanks for this part
                if (_partFuelTanks.TryGetValue(part.Id, out var fuelTanks))
                {
                    foreach (var tank in fuelTanks)
                    {
                        if (!tank.IsQueuedForDeletion())
                        {
                            _fuelTanks.Add(tank);
                        }
                    }
                }
            }
            
            Debug.Log($"PhysicsVessel {_vesselId}: Found {_engines.Count} engines and {_fuelTanks.Count} fuel tanks");
        }
        
        /// <summary>
        /// Get atmospheric pressure at given altitude (simple model)
        /// </summary>
        /// <param name="altitude">Altitude in meters</param>
        /// <returns>Atmospheric pressure in Pa</returns>
        private double GetAtmosphericPressure(double altitude)
        {
            var seaLevelPressure = 101325.0; // Pa
            var scaleHeight = 7500.0; // m
            
            return seaLevelPressure * FastMath.FastAtmosphericExp(altitude);
        }
        
        /// <summary>
        /// Process atmospheric forces including drag, heating effects, and dynamic pressure
        /// Integrates with atmospheric physics system for realistic flight dynamics
        /// </summary>
        /// <param name="delta">Physics timestep</param>
        private void ProcessAtmosphericForces(double delta)
        {
            var startTime = Time.GetTicksMsec();
            
            try
            {
                // Early exit if vessel has no parts
                if (_parts.Count == 0)
                    return;
                
                // Get atmospheric properties at vessel position
                var atmosphericProperties = Atmosphere.GetVesselAtmosphericProperties(this);
                
                // Apply aerodynamic drag forces to individual parts
                foreach (var vesselPart in _parts.Where(vp => vp.IsActive && vp.RigidBody != null && vp.PartReference != null))
                {
                    var part = vesselPart.PartReference!;
                    var velocity = vesselPart.RigidBody.LinearVelocity;
                    
                    // Calculate and apply drag force
                    var dragForce = AerodynamicDrag.CalculateDragForce(part, velocity, atmosphericProperties);
                    
                    if (dragForce.LengthSquared() > 1e-6)
                    {
                        vesselPart.RigidBody.ApplyCentralForce(dragForce);
                    }
                }
                
                // Update dynamic pressure tracking and auto-struts integration
                DynamicPressure.UpdateVesselQ(this);
                
                // Update all visual effects including heating
                EffectsManager.UpdateVesselEffects(this);
                
                // Performance monitoring
                var duration = Time.GetTicksMsec() - startTime;
                if (duration > 0.3) // Target: <0.3ms atmospheric processing per vessel
                {
                    Debug.LogWarning($"PhysicsVessel {_vesselId}: Atmospheric processing exceeded budget - {duration:F2}ms (target: 0.3ms)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"PhysicsVessel {_vesselId}: Atmospheric processing failed: {ex.Message}");
            }
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
            Debug.Log($"PhysicsVessel {_vesselId}: Cleared separation history");
        }
        
        /// <summary>
        /// Clean up vessel resources
        /// </summary>
        public void Cleanup()
        {
            _isActive = false;
            
            // Unregister from floating origin system
            if (_isOriginShiftRegistered)
            {
                FloatingOriginManager.UnregisterOriginShiftAware(this);
            }
            
            // Clean up anti-wobble system
            _antiWobbleSystem?.Reset();
            
            // Dispose all parts and joints properly
            Dispose();
            
            DebugLog.Log($"PhysicsVessel {_vesselId}: Cleaned up");
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
                Debug.LogWarning($"PhysicsVessel {_vesselId}: Orbital state update exceeded budget: {_orbitalCalculationTime:F3}ms");
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
                Debug.LogWarning($"PhysicsVessel {_vesselId}: Gravity calculation exceeded budget: {gravityCalcTime:F3}ms");
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
                
                Debug.Log($"PhysicsVessel {_vesselId}: Set to on-rails mode");
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
                
                Debug.Log($"PhysicsVessel {_vesselId}: Set to off-rails mode");
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
            
            Debug.Log($"PhysicsVessel {_vesselId}: Set to circular orbit at {altitude/1000:F1}km altitude");
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
        
        /// <summary>
        /// Get all engines for thrust calculations
        /// </summary>
        public IEnumerable<Engine> GetEngines()
        {
            return _engines;
        }
        
        /// <summary>
        /// Get current throttle setting
        /// </summary>
        public double GetThrottle()
        {
            return _currentThrottle;
        }
        
        /// <summary>
        /// Get atmospheric conditions at current position
        /// </summary>
        public AtmosphericState GetAtmosphericConditions()
        {
            var altitude = GlobalPosition.Y;
            var atmosphericPressure = GetAtmosphericPressure(altitude);
            var density = Atmosphere.GetDensity(altitude);
            var temperature = Atmosphere.GetTemperature(altitude);
            bool hasAtmosphere = altitude < UNIVERSE_CONSTANTS.KERBIN_ATMOSPHERE_HEIGHT;
            
            return new AtmosphericState
            {
                Altitude = altitude,
                Pressure = atmosphericPressure,
                Density = density,
                Temperature = temperature,
                HasAtmosphere = hasAtmosphere
            };
        }
        
        #region IOriginShiftAware Implementation
        
        /// <summary>
        /// Handle floating origin shift by updating vessel and part positions
        /// </summary>
        /// <param name="deltaPosition">The coordinate shift amount to apply</param>
        public void HandleOriginShift(Double3 deltaPosition)
        {
            if (!_isActive)
                return;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Update vessel position
                _position += deltaPosition;
                
                // Update center of mass
                _centerOfMass += deltaPosition;
                
                // Update orbital state if valid
                if (_orbitalStateValid && _orbitalState.HasValue)
                {
                    // The orbital mechanics position is relative to the celestial body,
                    // so we need to update the reference frame
                    var currentOrbitalState = _orbitalState.Value;
                    
                    // Update the epoch position to account for the coordinate shift
                    // This maintains the orbital mechanics accuracy across origin shifts
                    Double3 relativePosition = _position - _primaryBody.Position;
                    _orbitalState = OrbitalState.FromCartesian(relativePosition, _velocity, 
                                                              _lastOrbitalUpdate, 
                                                              _primaryBody.GravitationalParameter);
                }
                
                // Update all part positions
                foreach (var part in _parts)
                {
                    if (!part.IsActive || !part.IsUsable)
                        continue;
                    
                    // Update part positions by the delta amount
                    part.TryUseRigidBody(rigidBody =>
                    {
                        var currentPos = Double3.FromVector3(rigidBody.GlobalPosition);
                        var newPos = currentPos + deltaPosition;
                        rigidBody.GlobalPosition = newPos.ToVector3();
                    });
                    
                    // Update local position as well
                    part.LocalPosition += deltaPosition;
                }
                
                // Monitor distance for future origin shifts
                FloatingOriginManager.MonitorOriginDistance(_position);
                
                stopwatch.Stop();
                double shiftTime = stopwatch.Elapsed.TotalMilliseconds;
                
                if (shiftTime > 0.1) // 0.1ms target per vessel
                {
                    Debug.LogWarning($"PhysicsVessel {_vesselId}: Origin shift handling took {shiftTime:F3}ms (target: 0.1ms)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"PhysicsVessel {_vesselId}: Error handling origin shift: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Whether this vessel is registered for origin shift notifications
        /// </summary>
        public bool IsRegistered
        {
            get => _isOriginShiftRegistered;
            set => _isOriginShiftRegistered = value;
        }
        
        /// <summary>
        /// Priority for origin shift notifications (vessels are important systems)
        /// </summary>
        public int ShiftPriority => OriginShiftPriority.Important;
        
        /// <summary>
        /// Whether this vessel should receive origin shift notifications
        /// </summary>
        public bool ShouldReceiveOriginShifts => IsRegistered && _isActive;
        
        #endregion
        
        #region IDisposable Implementation
        
        private bool _disposed = false;
        
        /// <summary>
        /// Dispose of Godot resources to prevent memory leaks
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Dispose pattern implementation for proper resource cleanup
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Clean up parts using proper disposal
                foreach (var part in _parts)
                {
                    SafeOperations.SafeDispose(part, $"VesselPart[{part.Id}]");
                }
                _parts.Clear();
                
                // Clean up joints using proper disposal
                foreach (var joint in _joints)
                {
                    SafeOperations.SafeDispose(joint, $"VesselJoint[{joint.Id}]");
                }
                _joints.Clear();
                
                // Clear other collections
                _partLookup.Clear();
                _jointLookup.Clear();
                _engines.Clear();
                _fuelTanks.Clear();
                _partEngines.Clear();
                _partFuelTanks.Clear();
                
                _disposed = true;
            }
        }
        
        /// <summary>
        /// Override _ExitTree to ensure disposal
        /// </summary>
        public override void _ExitTree()
        {
            Dispose();
            base._ExitTree();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Individual vessel part data with proper lifecycle management
    /// </summary>
    public class VesselPart : IDisposable
    {
        public int Id;
        
        private ManagedGodotObject<RigidBody3D>? _managedRigidBody;
        private bool _disposed = false;
        
        public RigidBody3D? RigidBody => _managedRigidBody?.Object;
        
        public Part? PartReference; // Reference to the actual Part for atmospheric calculations
        public double Mass;
        public Double3 LocalPosition;
        public bool IsActive;
        
        // State tracking to eliminate repeated validation
        public bool IsDisposed => _disposed;
        public bool IsUsable => !_disposed && _managedRigidBody?.IsUsable == true;
        
        /// <summary>
        /// Safely sets the rigid body with lifecycle management
        /// </summary>
        public void SetRigidBody(RigidBody3D rigidBody)
        {
            if (rigidBody == null)
                throw new ArgumentNullException(nameof(rigidBody));
                
            // Dispose existing managed object if any
            _managedRigidBody?.Dispose();
            
            // Create new managed wrapper
            _managedRigidBody = SafeOperations.CreateManaged(rigidBody, $"VesselPart[{Id}].RigidBody");
            if (_managedRigidBody == null)
                throw new ArgumentException("Failed to create managed RigidBody - object is invalid", nameof(rigidBody));
        }
        
        /// <summary>
        /// Safely executes an action on the RigidBody if it's usable
        /// </summary>
        public bool TryUseRigidBody(Action<RigidBody3D> action)
        {
            return SafeOperations.TryUseManaged(_managedRigidBody, action, $"VesselPart[{Id}]");
        }
        
        /// <summary>
        /// Safely executes a function on the RigidBody if it's usable
        /// </summary>
        public TResult? TryUseRigidBody<TResult>(Func<RigidBody3D, TResult> func) where TResult : class
        {
            return _managedRigidBody?.TryExecute(func);
        }
        
        /// <summary>
        /// Safely executes a function on the RigidBody with a default value (reference types)
        /// </summary>
        public TResult TryUseRigidBodyWithDefault<TResult>(Func<RigidBody3D, TResult> func, TResult defaultValue) where TResult : class
        {
            return SafeOperations.TryUseManagedWithDefault(_managedRigidBody, func, defaultValue, $"VesselPart[{Id}]");
        }

        /// <summary>
        /// Safely executes a function on the RigidBody with a default value (value types)
        /// </summary>
        public TResult TryUseRigidBodyWithDefaultValue<TResult>(Func<RigidBody3D, TResult> func, TResult defaultValue) where TResult : struct
        {
            return SafeOperations.TryUseManagedWithDefaultValue(_managedRigidBody, func, defaultValue, $"VesselPart[{Id}]");
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            IsActive = false;
            
            _managedRigidBody?.Dispose();
            _managedRigidBody = null;
            
            DebugLog.LogResource($"VesselPart[{Id}] disposed");
        }
    }
    
    /// <summary>
    /// Joint between vessel parts with proper lifecycle management
    /// </summary>
    public class VesselJoint : IDisposable
    {
        public int Id;
        
        private ManagedGodotObject<Joint3D>? _managedJoint;
        private bool _disposed = false;
        
        public Joint3D? Joint => _managedJoint?.Object;
        
        public int PartA;
        public int PartB;
        public JointType JointType;
        public JointTuning Tuning;
        public bool IsActive;
        public double CurrentStress = 0.0; // Current force/torque applied to joint
        
        // State tracking to eliminate repeated validation
        public bool IsDisposed => _disposed;
        public bool IsUsable => !_disposed && _managedJoint?.IsUsable == true;
        
        /// <summary>
        /// Safely sets the joint with lifecycle management
        /// </summary>
        public void SetJoint(Joint3D joint)
        {
            if (joint == null)
                throw new ArgumentNullException(nameof(joint));
                
            // Dispose existing managed object if any
            _managedJoint?.Dispose();
            
            // Create new managed wrapper
            _managedJoint = SafeOperations.CreateManaged(joint, $"VesselJoint[{Id}].Joint");
            if (_managedJoint == null)
                throw new ArgumentException("Failed to create managed Joint - object is invalid", nameof(joint));
        }
        
        /// <summary>
        /// Safely executes an action on the Joint if it's usable
        /// </summary>
        public bool TryUseJoint(Action<Joint3D> action)
        {
            return SafeOperations.TryUseManaged(_managedJoint, action, $"VesselJoint[{Id}]");
        }
        
        /// <summary>
        /// Safely executes a function on the Joint if it's usable
        /// </summary>
        public TResult? TryUseJoint<TResult>(Func<Joint3D, TResult> func) where TResult : class
        {
            return _managedJoint?.TryExecute(func);
        }
        
        /// <summary>
        /// Safely executes a function on the Joint with a default value (reference types)
        /// </summary>
        public TResult TryUseJointWithDefault<TResult>(Func<Joint3D, TResult> func, TResult defaultValue) where TResult : class
        {
            return SafeOperations.TryUseManagedWithDefault(_managedJoint, func, defaultValue, $"VesselJoint[{Id}]");
        }

        /// <summary>
        /// Safely executes a function on the Joint with a default value (value types)
        /// </summary>
        public TResult TryUseJointWithDefaultValue<TResult>(Func<Joint3D, TResult> func, TResult defaultValue) where TResult : struct
        {
            return SafeOperations.TryUseManagedWithDefaultValue(_managedJoint, func, defaultValue, $"VesselJoint[{Id}]");
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            IsActive = false;
            
            _managedJoint?.Dispose();
            _managedJoint = null;
            
            DebugLog.LogResource($"VesselJoint[{Id}] disposed");
        }
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