extends Node

# Simple test script to validate joint separation mechanics
# Run this in Godot to test the separation system

func _ready():
	print("Starting joint separation validation test")
	test_separation_mechanics()

func test_separation_mechanics():
	print("=== Joint Separation Mechanics Test ===")
	
	# Create a physics vessel for testing
	var physics_vessel = preload("res://src/Core/PhysicsVessel.cs").new()
	physics_vessel.Initialize(1, null) # Test vessel ID 1, no physics manager for this test
	
	# Create test rigid bodies
	var bodies = []
	for i in range(3):
		var body = RigidBody3D.new()
		bodies.append(body)
		add_child(body)
		
		# Add part to vessel (mass in kg, position)
		var position = Vector3(0, i * 1.0, 0)
		var mass = 100.0 + i * 50.0  # Different masses
		physics_vessel.AddPart(body, mass, position)
	
	# Create separable joints
	print("Creating separable joints...")
	var joint1_success = physics_vessel.CreateJoint(0, 1, 3) # JointType.Separable
	var joint2_success = physics_vessel.CreateJoint(1, 2, 3) # JointType.Separable
	
	print("Joint 1 created: ", joint1_success)
	print("Joint 2 created: ", joint2_success)
	
	# Get initial mass properties
	var initial_mass = physics_vessel.GetMassProperties()
	print("Initial total mass: ", initial_mass.TotalMass, " kg")
	print("Initial joint count: ", physics_vessel.GetJointCount())
	
	# Test 1: Separate first joint
	print("\n--- Test 1: Separating joint 0 ---")
	var separation_success = physics_vessel.SeparateAtJoint(0, true, 500.0)
	print("Separation success: ", separation_success)
	
	var post_sep_mass = physics_vessel.GetMassProperties()
	print("Post-separation mass: ", post_sep_mass.TotalMass, " kg")
	print("Post-separation joint count: ", physics_vessel.GetJointCount())
	
	# Get separation metrics
	var metrics = physics_vessel.GetLatestSeparationMetrics()
	if metrics != null:
		print("Operation time: ", metrics.OperationTime, " ms")
		print("Success: ", metrics.Success)
		print("Mass ratio: ", metrics.MassRatio)
	else:
		print("No separation metrics available")
	
	# Wait a frame for physics to settle
	await get_tree().process_frame
	
	# Test 2: Separate second joint if still available
	if physics_vessel.GetJointCount() > 0:
		print("\n--- Test 2: Separating joint 1 ---")
		var separation_success2 = physics_vessel.SeparateAtJoint(1, true, 500.0)
		print("Second separation success: ", separation_success2)
		
		var final_mass = physics_vessel.GetMassProperties()
		print("Final mass: ", final_mass.TotalMass, " kg")
		print("Final joint count: ", physics_vessel.GetJointCount())
		
		var metrics2 = physics_vessel.GetLatestSeparationMetrics()
		if metrics2 != null:
			print("Second operation time: ", metrics2.OperationTime, " ms")
		
		await get_tree().process_frame
	
	# Get performance statistics
	var perf_stats = physics_vessel.GetSeparationPerformanceStats()
	print("\n--- Performance Statistics ---")
	print("Total separations: ", perf_stats.TotalSeparations)
	print("Successful separations: ", perf_stats.SuccessfulSeparations)
	print("Success rate: ", perf_stats.SuccessRate * 100, "%")
	print("Average operation time: ", perf_stats.AverageOperationTime, " ms")
	print("Max operation time: ", perf_stats.MaxOperationTime, " ms")
	
	# Test 3: Rapid successive separations (if we add more joints)
	print("\n--- Test 3: Testing separation system stability ---")
	print("Adding more parts for stability testing...")
	
	# Add more parts for stability testing
	for i in range(3, 8): # Add 5 more parts
		var body = RigidBody3D.new()
		add_child(body)
		var position = Vector3(0, i * 1.0, 0)
		physics_vessel.AddPart(body, 75.0, position) # 75kg parts
		
		# Create joint to previous part
		if i > 3:
			physics_vessel.CreateJoint(i-1, i, 3) # Separable joints
	
	# Test rapid separations
	var rapid_test_results = []
	for test_num in range(1, 6): # Up to 5 rapid separations
		var joint_count = physics_vessel.GetJointCount()
		if joint_count == 0:
			break
			
		print("Rapid separation test ", test_num)
		var start_time = Time.get_ticks_usec()
		var success = physics_vessel.SeparateAtJoint(0, true, 500.0) # Always separate first available
		var end_time = Time.get_ticks_usec()
		var duration = (end_time - start_time) / 1000.0 # Convert to ms
		
		rapid_test_results.append({
			"test": test_num,
			"success": success,
			"duration": duration
		})
		
		# Wait minimal time between tests
		await get_tree().create_timer(0.01).timeout
	
	# Report rapid test results
	print("\n--- Rapid Separation Test Results ---")
	for result in rapid_test_results:
		var status = "PASS" if result.success else "FAIL"
		var time_status = "FAST" if result.duration < 0.2 else "SLOW"
		print("Test ", result.test, ": ", status, " (", result.duration, "ms) - ", time_status)
	
	# Final performance stats
	var final_perf_stats = physics_vessel.GetSeparationPerformanceStats()
	print("\n--- Final Performance Report ---")
	print("Total operations: ", final_perf_stats.TotalSeparations)
	print("Success rate: ", final_perf_stats.SuccessRate * 100, "%")
	print("Average time: ", final_perf_stats.AverageOperationTime, " ms")
	print("Performance target (<0.2ms): ", "PASS" if final_perf_stats.AverageOperationTime < 0.2 else "FAIL")
	
	# Cleanup
	physics_vessel.Cleanup()
	for body in bodies:
		body.queue_free()
		
	print("\n=== Joint Separation Test Complete ===")