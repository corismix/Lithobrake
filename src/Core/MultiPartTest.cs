using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Simple multi-part vessel test for validation
    /// Tests basic functionality without complex scene dependencies
    /// </summary>
    public partial class MultiPartTest : Node3D
    {
        private PhysicsManager? _physicsManager;
        private PhysicsVessel? _testVessel;
        
        public override void _Ready()
        {
            GD.Print("MultiPartTest: Starting validation tests");
            RunTests();
        }
        
        private async void RunTests()
        {
            // Wait a frame for initialization
            await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
            
            bool allTestsPassed = true;
            
            // Test 1: Create Physics Manager
            allTestsPassed &= TestPhysicsManagerCreation();
            
            // Test 2: Create Multi-Part Vessel
            allTestsPassed &= TestVesselCreation();
            
            // Test 3: Add Parts and Joints
            allTestsPassed &= TestPartAndJointCreation();
            
            // Test 4: Mass Properties
            allTestsPassed &= TestMassProperties();
            
            // Test 5: Part Removal
            allTestsPassed &= TestPartRemoval();
            
            // Report results
            if (allTestsPassed)
            {
                GD.Print("✅ ALL MULTI-PART TESTS PASSED!");
            }
            else
            {
                GD.PrintErr("❌ Some multi-part tests failed");
            }
            
            // Cleanup
            Cleanup();
        }
        
        private bool TestPhysicsManagerCreation()
        {
            GD.Print("\n--- Test 1: Physics Manager Creation ---");
            
            _physicsManager = new PhysicsManager();
            AddChild(_physicsManager);
            
            if (_physicsManager == null)
            {
                GD.PrintErr("Failed to create PhysicsManager");
                return false;
            }
            
            GD.Print("✅ PhysicsManager created successfully");
            return true;
        }
        
        private bool TestVesselCreation()
        {
            GD.Print("\n--- Test 2: Vessel Creation ---");
            
            if (_physicsManager == null)
            {
                GD.PrintErr("PhysicsManager not available");
                return false;
            }
            
            _testVessel = new PhysicsVessel();
            AddChild(_testVessel);
            
            var vesselId = _physicsManager.RegisterVessel(_testVessel);
            
            if (vesselId <= 0)
            {
                GD.PrintErr("Failed to register vessel");
                return false;
            }
            
            GD.Print($"✅ Vessel created and registered with ID {vesselId}");
            return true;
        }
        
        private bool TestPartAndJointCreation()
        {
            GD.Print("\n--- Test 3: Part and Joint Creation ---");
            
            if (_testVessel == null)
            {
                GD.PrintErr("Test vessel not available");
                return false;
            }
            
            // Create 3 parts for testing
            var parts = new RigidBody3D[3];
            for (int i = 0; i < 3; i++)
            {
                parts[i] = CreateTestPart(i);
                AddChild(parts[i]);
                
                var mass = 100.0;
                var localPos = new Double3(0, i * 0.6, 0);
                
                if (!_testVessel.AddPart(parts[i], mass, localPos))
                {
                    GD.PrintErr($"Failed to add part {i}");
                    return false;
                }
            }
            
            // Create joints between parts
            for (int i = 0; i < 2; i++)
            {
                if (!_testVessel.CreateJoint(i, i + 1, JointType.Fixed))
                {
                    GD.PrintErr($"Failed to create joint {i}-{i + 1}");
                    return false;
                }
            }
            
            var partCount = _testVessel.GetPartCount();
            var jointCount = _testVessel.GetJointCount();
            
            if (partCount != 3)
            {
                GD.PrintErr($"Expected 3 parts, got {partCount}");
                return false;
            }
            
            if (jointCount != 2)
            {
                GD.PrintErr($"Expected 2 joints, got {jointCount}");
                return false;
            }
            
            GD.Print($"✅ Created 3 parts and 2 joints successfully");
            return true;
        }
        
        private bool TestMassProperties()
        {
            GD.Print("\n--- Test 4: Mass Properties ---");
            
            if (_testVessel == null)
            {
                GD.PrintErr("Test vessel not available");
                return false;
            }
            
            var massProperties = _testVessel.GetMassProperties();
            var expectedMass = 300.0; // 3 parts * 100kg each
            
            if (Math.Abs(massProperties.TotalMass - expectedMass) > 1.0)
            {
                GD.PrintErr($"Mass calculation incorrect: expected {expectedMass}kg, got {massProperties.TotalMass:F1}kg");
                return false;
            }
            
            // Center of mass should be around middle part
            var expectedComY = 0.6; // Middle of 3-part stack (0, 0.6, 1.2)
            if (Math.Abs(massProperties.CenterOfMass.Y - expectedComY) > 0.1)
            {
                GD.PrintErr($"Center of mass Y incorrect: expected {expectedComY:F1}, got {massProperties.CenterOfMass.Y:F1}");
                return false;
            }
            
            GD.Print($"✅ Mass properties correct: {massProperties.TotalMass:F0}kg total, CoM at Y={massProperties.CenterOfMass.Y:F1}");
            return true;
        }
        
        private bool TestPartRemoval()
        {
            GD.Print("\n--- Test 5: Part Removal ---");
            
            if (_testVessel == null)
            {
                GD.PrintErr("Test vessel not available");
                return false;
            }
            
            var initialParts = _testVessel.GetPartCount();
            var initialJoints = _testVessel.GetJointCount();
            
            // Remove top part (part 2)
            if (!_testVessel.RemovePart(2, false))
            {
                GD.PrintErr("Failed to remove part 2");
                return false;
            }
            
            var finalParts = _testVessel.GetPartCount();
            var finalJoints = _testVessel.GetJointCount();
            
            if (finalParts != initialParts - 1)
            {
                GD.PrintErr($"Part removal failed: expected {initialParts - 1} parts, got {finalParts}");
                return false;
            }
            
            if (finalJoints != initialJoints - 1)
            {
                GD.PrintErr($"Joint cleanup failed: expected {initialJoints - 1} joints, got {finalJoints}");
                return false;
            }
            
            // Check mass update
            var newMassProperties = _testVessel.GetMassProperties();
            var expectedNewMass = 200.0; // 2 parts * 100kg each
            
            if (Math.Abs(newMassProperties.TotalMass - expectedNewMass) > 1.0)
            {
                GD.PrintErr($"Mass not updated after removal: expected {expectedNewMass}kg, got {newMassProperties.TotalMass:F1}kg");
                return false;
            }
            
            GD.Print($"✅ Part removal successful: {initialParts}→{finalParts} parts, {initialJoints}→{finalJoints} joints, mass updated to {newMassProperties.TotalMass:F0}kg");
            return true;
        }
        
        private RigidBody3D CreateTestPart(int id)
        {
            var rigidBody = new RigidBody3D();
            var collisionShape = new CollisionShape3D();
            var boxShape = new BoxShape3D();
            var meshInstance = new MeshInstance3D();
            var boxMesh = new BoxMesh();
            
            // Set up geometry
            boxShape.Size = Vector3.One * 0.5f;
            boxMesh.Size = Vector3.One * 0.5f;
            
            collisionShape.Shape = boxShape;
            meshInstance.Mesh = boxMesh;
            
            rigidBody.AddChild(collisionShape);
            rigidBody.AddChild(meshInstance);
            
            // Position part
            rigidBody.Position = new Vector3(0, id * 0.6f, 0);
            rigidBody.Mass = 100.0f;
            
            return rigidBody;
        }
        
        private void Cleanup()
        {
            GD.Print("\nCleaning up test...");
            
            if (_testVessel != null)
            {
                _testVessel.Cleanup();
            }
        }
    }
}