using Godot;
using System;
using System.Collections.Generic;

namespace Lithobrake.Core
{
    /// <summary>
    /// Base class for all rocket parts in the game.
    /// Provides common properties and methods for mass calculation, attachment points,
    /// and part lifecycle management.
    /// </summary>
    public abstract partial class Part : Node3D
    {
        // Core part properties
        public double DryMass { get; protected set; }
        public double FuelMass { get; set; }
        public string PartId { get; protected set; } = string.Empty;
        public string PartName { get; protected set; } = string.Empty;
        public string Description { get; protected set; } = string.Empty;
        public PartType Type { get; protected set; }
        
        // Attachment points
        public AttachmentNode? AttachTop { get; protected set; }
        public AttachmentNode? AttachBottom { get; protected set; }
        public List<AttachmentNode> RadialAttachPoints { get; private set; } = new();
        
        // Visual and physics components
        public MeshInstance3D? MeshInstance { get; private set; }
        public RigidBody3D? RigidBody { get; private set; }
        public CollisionShape3D? CollisionShape { get; private set; }
        
        // Part state
        public bool IsInitialized { get; private set; } = false;
        public bool IsAttached { get; set; } = false;
        public Part? AttachedParent { get; set; }
        public List<Part> AttachedChildren { get; private set; } = new();
        
        // Performance tracking
        private static PerformanceMonitor? _performanceMonitor;
        
        public override void _Ready()
        {
            _performanceMonitor = PerformanceMonitor.Instance;
            
            // Initialize the part if not already done
            if (!IsInitialized)
            {
                Initialize();
            }
        }
        
        /// <summary>
        /// Initialize the part with its properties and components.
        /// Must be implemented by derived classes.
        /// </summary>
        public virtual void Initialize()
        {
            var startTime = Time.GetTicksMsec();
            
            try
            {
                // Create physics body if needed
                if (RigidBody == null)
                {
                    CreatePhysicsBody();
                }
                
                // Create visual representation if needed
                if (MeshInstance == null)
                {
                    CreateMeshInstance();
                }
                
                // Initialize attachment nodes
                InitializeAttachmentNodes();
                
                // Mark as initialized
                IsInitialized = true;
                
                // Performance tracking
                var duration = Time.GetTicksMsec() - startTime;
                if (duration > 1.0) // Target: <1ms per part creation
                {
                    GD.PrintErr($"Part.Initialize took {duration:F1}ms (target: <1ms) for {PartName}");
                }
                
                GD.Print($"Part initialized: {PartName} ({GetTotalMass():F0}kg)");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Part.Initialize failed for {PartName}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Get the total mass of the part (dry mass + fuel mass).
        /// </summary>
        public virtual double GetTotalMass()
        {
            return DryMass + FuelMass;
        }
        
        /// <summary>
        /// Create physics body for the part.
        /// </summary>
        protected virtual void CreatePhysicsBody()
        {
            RigidBody = new RigidBody3D
            {
                Name = $"{PartName}_RigidBody",
                Mass = (float)GetTotalMass(),
                GravityScale = 1.0f
            };
            
            AddChild(RigidBody);
            
            // Create collision shape
            CollisionShape = new CollisionShape3D
            {
                Name = $"{PartName}_Collision"
            };
            
            // Use box shape as default - can be overridden by derived classes
            var boxShape = new BoxShape3D
            {
                Size = GetPartDimensions()
            };
            CollisionShape.Shape = boxShape;
            
            RigidBody.AddChild(CollisionShape);
        }
        
        /// <summary>
        /// Create mesh instance for visual representation.
        /// </summary>
        protected virtual void CreateMeshInstance()
        {
            MeshInstance = new MeshInstance3D
            {
                Name = $"{PartName}_Mesh"
            };
            
            // Load mesh from resources or create primitive fallback
            var mesh = LoadPartMesh();
            if (mesh != null)
            {
                MeshInstance.Mesh = mesh;
            }
            else
            {
                // Create primitive fallback
                MeshInstance.Mesh = CreatePrimitiveMesh();
                GD.PrintErr($"Part.CreateMeshInstance: Using primitive fallback for {PartName}");
            }
            
            AddChild(MeshInstance);
        }
        
        /// <summary>
        /// Load part mesh from resources. Can be overridden by derived classes.
        /// </summary>
        protected virtual Mesh? LoadPartMesh()
        {
            // Try to load from resources/parts/meshes/
            var meshPath = $"res://resources/parts/meshes/{PartId.ToLower()}.obj";
            
            if (ResourceLoader.Exists(meshPath))
            {
                return GD.Load<Mesh>(meshPath);
            }
            
            // Try .glb format
            meshPath = $"res://resources/parts/meshes/{PartId.ToLower()}.glb";
            if (ResourceLoader.Exists(meshPath))
            {
                return GD.Load<Mesh>(meshPath);
            }
            
            return null;
        }
        
        /// <summary>
        /// Create primitive mesh fallback. Must be implemented by derived classes.
        /// </summary>
        protected abstract Mesh CreatePrimitiveMesh();
        
        /// <summary>
        /// Get part dimensions for collision shape. Must be implemented by derived classes.
        /// </summary>
        protected abstract Vector3 GetPartDimensions();
        
        /// <summary>
        /// Initialize attachment nodes. Can be overridden by derived classes.
        /// </summary>
        protected virtual void InitializeAttachmentNodes()
        {
            // Default implementation - create basic top/bottom nodes
            var dimensions = GetPartDimensions();
            
            if (AttachTop == null)
            {
                AttachTop = new AttachmentNode
                {
                    Position = new Vector3(0, dimensions.Y / 2, 0),
                    Orientation = Vector3.Up,
                    MaxMass = GetMaxAttachmentMass(),
                    IsOccupied = false
                };
            }
            
            if (AttachBottom == null)
            {
                AttachBottom = new AttachmentNode
                {
                    Position = new Vector3(0, -dimensions.Y / 2, 0),
                    Orientation = Vector3.Down,
                    MaxMass = GetMaxAttachmentMass(),
                    IsOccupied = false
                };
            }
        }
        
        /// <summary>
        /// Get maximum mass that can be attached to this part.
        /// Can be overridden by derived classes.
        /// </summary>
        protected virtual double GetMaxAttachmentMass()
        {
            // Default: allow 10x part's own mass
            return GetTotalMass() * 10.0;
        }
        
        /// <summary>
        /// Attach another part to this part at specified attachment point.
        /// </summary>
        public virtual bool AttachPart(Part childPart, AttachmentNode attachmentNode)
        {
            if (attachmentNode.IsOccupied)
            {
                GD.PrintErr($"Part.AttachPart: Attachment node already occupied on {PartName}");
                return false;
            }
            
            if (childPart.GetTotalMass() > attachmentNode.MaxMass)
            {
                GD.PrintErr($"Part.AttachPart: Child part too heavy ({childPart.GetTotalMass():F0}kg > {attachmentNode.MaxMass:F0}kg)");
                return false;
            }
            
            // Set up parent/child relationship
            childPart.AttachedParent = this;
            AttachedChildren.Add(childPart);
            childPart.IsAttached = true;
            
            // Mark attachment node as occupied
            attachmentNode.IsOccupied = true;
            
            // Position child part at attachment point
            childPart.GlobalPosition = GlobalPosition + attachmentNode.Position;
            
            GD.Print($"Part.AttachPart: Attached {childPart.PartName} to {PartName}");
            return true;
        }
        
        /// <summary>
        /// Detach a child part from this part.
        /// </summary>
        public virtual bool DetachPart(Part childPart)
        {
            if (!AttachedChildren.Contains(childPart))
            {
                GD.PrintErr($"Part.DetachPart: {childPart.PartName} not attached to {PartName}");
                return false;
            }
            
            // Remove parent/child relationship
            childPart.AttachedParent = null;
            AttachedChildren.Remove(childPart);
            childPart.IsAttached = false;
            
            // Free up attachment node
            FreeAttachmentNode(childPart);
            
            GD.Print($"Part.DetachPart: Detached {childPart.PartName} from {PartName}");
            return true;
        }
        
        /// <summary>
        /// Find and free the attachment node used by a child part.
        /// </summary>
        private void FreeAttachmentNode(Part childPart)
        {
            // Check top attachment
            if (AttachTop?.IsOccupied == true)
            {
                var distance = (childPart.GlobalPosition - (GlobalPosition + AttachTop.Position)).Length();
                if (distance < 0.1f) // Within 10cm
                {
                    AttachTop.IsOccupied = false;
                    return;
                }
            }
            
            // Check bottom attachment
            if (AttachBottom?.IsOccupied == true)
            {
                var distance = (childPart.GlobalPosition - (GlobalPosition + AttachBottom.Position)).Length();
                if (distance < 0.1f)
                {
                    AttachBottom.IsOccupied = false;
                    return;
                }
            }
            
            // Check radial attachments
            foreach (var radialNode in RadialAttachPoints)
            {
                if (radialNode.IsOccupied)
                {
                    var distance = (childPart.GlobalPosition - (GlobalPosition + radialNode.Position)).Length();
                    if (distance < 0.1f)
                    {
                        radialNode.IsOccupied = false;
                        return;
                    }
                }
            }
        }
        
        /// <summary>
        /// Calculate center of mass for this part and all attached children.
        /// </summary>
        public virtual Vector3 CalculateCenterOfMass()
        {
            var totalMass = GetTotalMass();
            var com = GlobalPosition * (float)totalMass;
            
            foreach (var child in AttachedChildren)
            {
                var childMass = child.GetTotalMass();
                var childCom = child.CalculateCenterOfMass();
                
                com += childCom * (float)childMass;
                totalMass += childMass;
            }
            
            if (totalMass > 0)
            {
                com /= (float)totalMass;
            }
            
            return com;
        }
        
        /// <summary>
        /// Get all parts in this part tree (this part + all descendants).
        /// </summary>
        public virtual List<Part> GetAllParts()
        {
            var allParts = new List<Part> { this };
            
            foreach (var child in AttachedChildren)
            {
                allParts.AddRange(child.GetAllParts());
            }
            
            return allParts;
        }
        
        /// <summary>
        /// Update physics properties based on current state.
        /// </summary>
        public virtual void UpdatePhysicsProperties()
        {
            if (RigidBody != null)
            {
                RigidBody.Mass = (float)GetTotalMass();
                
                // Update center of mass if needed
                var com = CalculateCenterOfMass() - GlobalPosition;
                RigidBody.CenterOfMass = com;
            }
        }
        
        /// <summary>
        /// Cleanup part resources.
        /// </summary>
        public override void _ExitTree()
        {
            // Detach from parent if needed
            if (AttachedParent != null && IsAttached)
            {
                AttachedParent.DetachPart(this);
            }
            
            // Detach all children
            var childrenCopy = new List<Part>(AttachedChildren);
            foreach (var child in childrenCopy)
            {
                DetachPart(child);
            }
            
            base._ExitTree();
        }
        
        /// <summary>
        /// Get part information as string for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"{PartName} ({PartId}): {GetTotalMass():F1}kg, Children: {AttachedChildren.Count}";
        }
    }
}