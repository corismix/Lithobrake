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
            
            GD.Print($"PhysicsVessel {_vesselId}: Initialized");
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
        /// Clean up vessel resources
        /// </summary>
        public void Cleanup()
        {
            _isActive = false;
            
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
}