extends Node3D

## Comprehensive test runner for object lifecycle management system
## Coordinates both unit tests and stress tests with detailed reporting

@onready var results_label: Label = $UI/MainContainer/ResultsContainer/ResultsLabel
@onready var run_all_button: Button = $UI/MainContainer/ButtonContainer/RunAllTests
@onready var run_unit_button: Button = $UI/MainContainer/ButtonContainer/RunUnitTests
@onready var run_stress_button: Button = $UI/MainContainer/ButtonContainer/RunStressTests
@onready var clear_button: Button = $UI/MainContainer/ButtonContainer/ClearResults

var stress_tester: Node
var test_wrapper: Node
var test_results: String = ""

func _ready():
	print("LifecycleTestRunner: Initializing comprehensive test suite...")
	
	# Create the C# stress tester
	var LifecycleStressTester = load("res://src/Core/LifecycleStressTester.cs")
	if LifecycleStressTester:
		stress_tester = LifecycleStressTester.new()
		add_child(stress_tester)
		print("LifecycleTestRunner: Stress tester loaded successfully")
	else:
		print("LifecycleTestRunner: Failed to load LifecycleStressTester")
	
	# Create the C# test wrapper
	var LifecycleTestWrapper = load("res://src/Core/LifecycleTestWrapper.cs")
	if LifecycleTestWrapper:
		test_wrapper = LifecycleTestWrapper.new()
		add_child(test_wrapper)
		print("LifecycleTestRunner: Test wrapper loaded successfully")
	else:
		print("LifecycleTestRunner: Failed to load LifecycleTestWrapper")
	
	if stress_tester and test_wrapper:
		update_results("Object Lifecycle Management Test Suite Ready\n\nClick 'Run All Tests' for comprehensive validation or choose specific test categories.")
	else:
		update_results("ERROR: Could not load test infrastructure!")
		print("LifecycleTestRunner: Failed to load test infrastructure")

func _on_run_all_tests_pressed():
	print("LifecycleTestRunner: Starting comprehensive test suite...")
	disable_buttons()
	update_results("Running comprehensive lifecycle management tests...\n\nThis may take several minutes.")
	
	# Allow UI to update
	await get_tree().process_frame
	
	var all_results = ""
	
	# Run unit tests first
	print("LifecycleTestRunner: Running unit tests...")
	var unit_results = run_unit_tests_internal()
	all_results += unit_results + "\n\n"
	
	# Allow UI to update between test phases
	await get_tree().process_frame
	update_results(all_results + "Unit tests complete. Running stress tests...")
	
	# Run stress tests
	if stress_tester:
		print("LifecycleTestRunner: Running stress tests...")
		var stress_results = stress_tester.RunStressTest(1000)
		all_results += "=== STRESS TEST RESULTS ===\n" + stress_results + "\n\n"
		
		await get_tree().process_frame
		update_results(all_results + "Stress tests complete. Running memory leak test...")
		
		var memory_results = stress_tester.RunMemoryLeakTest()
		all_results += "=== MEMORY LEAK TEST RESULTS ===\n" + memory_results + "\n\n"
		
		await get_tree().process_frame
		update_results(all_results + "Memory tests complete. Running concurrency test...")
		
		var concurrency_results = stress_tester.RunConcurrencyTest()
		all_results += "=== CONCURRENCY TEST RESULTS ===\n" + concurrency_results
	else:
		all_results += "ERROR: Stress tester not available!"
	
	# Add final summary
	all_results += "\n\n=== COMPREHENSIVE TEST SUMMARY ===\n"
	all_results += "All lifecycle management tests completed.\n"
	all_results += "Review results above for any issues.\n"
	all_results += "Time: " + Time.get_datetime_string_from_system()
	
	update_results(all_results)
	enable_buttons()
	print("LifecycleTestRunner: Comprehensive test suite completed")

func _on_run_unit_tests_pressed():
	print("LifecycleTestRunner: Running unit tests...")
	disable_buttons()
	update_results("Running unit tests...")
	
	await get_tree().process_frame
	var results = run_unit_tests_internal()
	
	update_results(results)
	enable_buttons()
	print("LifecycleTestRunner: Unit tests completed")

func _on_run_stress_tests_pressed():
	if not stress_tester:
		update_results("ERROR: Stress tester not available!")
		return
	
	print("LifecycleTestRunner: Running stress tests...")
	disable_buttons()
	update_results("Running stress tests (this may take several minutes)...")
	
	await get_tree().process_frame
	
	var results = ""
	
	# Run stress test
	var stress_results = stress_tester.RunStressTest(1000)
	results += "=== STRESS TEST ===\n" + stress_results + "\n\n"
	
	await get_tree().process_frame
	update_results(results + "Running memory leak test...")
	
	# Run memory test
	var memory_results = stress_tester.RunMemoryLeakTest()
	results += "=== MEMORY LEAK TEST ===\n" + memory_results + "\n\n"
	
	await get_tree().process_frame
	update_results(results + "Running concurrency test...")
	
	# Run concurrency test
	var concurrency_results = stress_tester.RunConcurrencyTest()
	results += "=== CONCURRENCY TEST ===\n" + concurrency_results
	
	update_results(results)
	enable_buttons()
	print("LifecycleTestRunner: Stress tests completed")

func _on_clear_results_pressed():
	update_results("Test results cleared.")
	test_results = ""
	print("LifecycleTestRunner: Results cleared")

func run_unit_tests_internal() -> String:
	if not test_wrapper:
		return "ERROR: Test wrapper not available!"
	
	print("LifecycleTestRunner: Calling C# unit tests...")
	
	# First validate core systems integration
	var integration_results = test_wrapper.ValidateCoreSystemsIntegration()
	
	# Then run comprehensive unit tests
	var unit_test_results = test_wrapper.RunAllLifecycleTests()
	
	# Get current statistics
	var stats = test_wrapper.GetLifecycleStatistics()
	
	var combined_results = integration_results + "\n\n" + unit_test_results + "\n\nCurrent Statistics: " + stats
	
	print("LifecycleTestRunner: Unit tests completed")
	return combined_results

func update_results(text: String):
	test_results = text
	if results_label:
		results_label.text = text
	print("LifecycleTestRunner: " + text.split("\n")[0])  # Print first line to console

func disable_buttons():
	run_all_button.disabled = true
	run_unit_button.disabled = true
	run_stress_button.disabled = true

func enable_buttons():
	run_all_button.disabled = false
	run_unit_button.disabled = false
	run_stress_button.disabled = false

func _exit_tree():
	print("LifecycleTestRunner: Shutting down test suite")
	if stress_tester:
		stress_tester.queue_free()
	if test_wrapper:
		test_wrapper.queue_free()