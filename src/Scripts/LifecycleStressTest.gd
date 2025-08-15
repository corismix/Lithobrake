extends Node3D

## Lifecycle stress test controller
## Coordinates stress testing of the object lifecycle management system

@onready var results_label: Label = $UI/TestControls/ResultsLabel
@onready var run_stress_button: Button = $UI/TestControls/RunStressTest
@onready var run_memory_button: Button = $UI/TestControls/RunMemoryTest
@onready var run_concurrency_button: Button = $UI/TestControls/RunConcurrencyTest

var stress_tester: Node

func _ready():
	# Create the C# stress tester
	var LifecycleStressTester = load("res://src/Core/LifecycleStressTester.cs")
	if LifecycleStressTester:
		stress_tester = LifecycleStressTester.new()
		add_child(stress_tester)
		update_results("Lifecycle stress tester initialized.")
	else:
		update_results("ERROR: Could not load LifecycleStressTester!")

func _on_run_stress_test_pressed():
	if not stress_tester:
		update_results("ERROR: Stress tester not available!")
		return
		
	disable_buttons()
	update_results("Running stress test (1000 cycles)...")
	
	# Run stress test in background
	await get_tree().process_frame
	var result = stress_tester.RunStressTest(1000)
	
	update_results(result)
	enable_buttons()

func _on_run_memory_test_pressed():
	if not stress_tester:
		update_results("ERROR: Stress tester not available!")
		return
		
	disable_buttons()
	update_results("Running memory leak test...")
	
	# Run memory test in background
	await get_tree().process_frame
	var result = stress_tester.RunMemoryLeakTest()
	
	update_results(result)
	enable_buttons()

func _on_run_concurrency_test_pressed():
	if not stress_tester:
		update_results("ERROR: Stress tester not available!")
		return
		
	disable_buttons()
	update_results("Running concurrency test...")
	
	# Run concurrency test in background
	await get_tree().process_frame
	var result = stress_tester.RunConcurrencyTest()
	
	update_results(result)
	enable_buttons()

func _on_clear_results_pressed():
	update_results("Results cleared.")

func update_results(text: String):
	if results_label:
		results_label.text = text
		print("LifecycleStressTest: " + text)

func disable_buttons():
	run_stress_button.disabled = true
	run_memory_button.disabled = true
	run_concurrency_button.disabled = true

func enable_buttons():
	run_stress_button.disabled = false
	run_memory_button.disabled = false
	run_concurrency_button.disabled = false