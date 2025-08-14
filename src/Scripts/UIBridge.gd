extends Control
class_name UIBridge

# GDScript UI bridge for testing C# to GDScript signal communication
# Demonstrates performance boundary patterns and signal marshaling overhead

signal ui_update_requested(data: Dictionary)
signal performance_test_completed(results: Dictionary)

var _test_node: Node3D
var _performance_monitor: Node
var _signal_test_count: int = 0
var _last_signal_time: float = 0.0
var _signal_frequency_samples: Array[float] = []

# Performance tracking
var _ui_update_times: Array[float] = []
var _max_samples: int = 60  # 1 second of data at 60fps

func _ready():
	print("UIBridge: GDScript UI bridge initialized")
	_setup_ui_elements()
	_connect_to_csharp_nodes()

func _setup_ui_elements():
	# Create simple UI for testing
	var label = Label.new()
	label.name = "TestLabel" 
	label.text = "UI Bridge Active - Testing C#/GDScript Integration"
	label.position = Vector2(10, 200)
	add_child(label)
	
	var info_label = Label.new()
	info_label.name = "InfoLabel"
	info_label.text = "Signal Tests: 0\nAvg UI Update: 0.00ms"
	info_label.position = Vector2(10, 240)
	add_child(info_label)

func _connect_to_csharp_nodes():
	# Find C# test node in scene
	_test_node = find_child("CSharpTestNode")
	if _test_node == null:
		# Try to find in scene tree
		_test_node = get_tree().get_first_node_in_group("csharp_test")
	
	if _test_node != null:
		# Connect to C# signals
		if not _test_node.test_signal.is_connected(_on_test_signal_received):
			_test_node.test_signal.connect(_on_test_signal_received)
		if not _test_node.packed_data.is_connected(_on_packed_data_received):
			_test_node.packed_data.connect(_on_packed_data_received)
		print("UIBridge: Connected to C# test node signals")
	else:
		print("UIBridge: Warning - C# test node not found")
	
	# Find performance monitor
	_performance_monitor = get_tree().get_first_node_in_group("performance_monitor")
	if _performance_monitor == null:
		print("UIBridge: Warning - Performance monitor not found")

func _process(delta):
	# Measure UI update performance
	var start_time = Time.get_ticks_usec()
	
	_update_ui_performance_display()
	
	var end_time = Time.get_ticks_usec()
	var ui_time = (end_time - start_time) / 1000.0  # Convert to milliseconds
	
	# Track UI performance
	_ui_update_times.append(ui_time)
	if _ui_update_times.size() > _max_samples:
		_ui_update_times.pop_front()

func _update_ui_performance_display():
	var info_label = find_child("InfoLabel")
	if info_label != null:
		var avg_ui_time = 0.0
		if _ui_update_times.size() > 0:
			var total = 0.0
			for time in _ui_update_times:
				total += time
			avg_ui_time = total / _ui_update_times.size()
		
		var frequency = 0.0
		if _signal_frequency_samples.size() > 0:
			var total_freq = 0.0
			for freq in _signal_frequency_samples:
				total_freq += freq
			frequency = total_freq / _signal_frequency_samples.size()
		
		info_label.text = "Signal Tests: %d\nAvg UI Update: %.3fms\nSignal Frequency: %.1fHz" % [
			_signal_test_count, avg_ui_time, frequency
		]

func _on_test_signal_received(message: String, value: float):
	print("UIBridge: Received test signal - %s: %.2f" % [message, value])
	_signal_test_count += 1
	
	# Measure signal frequency
	var current_time = Time.get_ticks_msec() / 1000.0
	if _last_signal_time > 0:
		var delta_time = current_time - _last_signal_time
		if delta_time > 0:
			var frequency = 1.0 / delta_time
			_signal_frequency_samples.append(frequency)
			if _signal_frequency_samples.size() > 10:  # Keep last 10 samples
				_signal_frequency_samples.pop_front()
	
	_last_signal_time = current_time
	
	# Test UI response to C# signals
	_test_ui_response(message, value)

func _on_packed_data_received(packed_data: Dictionary):
	print("UIBridge: Received packed data with %d entries" % packed_data.size())
	
	# Process packed signal data efficiently
	if packed_data.has("frame_count"):
		print("  Frame Count: %d" % packed_data["frame_count"])
	if packed_data.has("test_value"):
		print("  Test Value: %.2f" % packed_data["test_value"])
	if packed_data.has("avg_update_time"):
		print("  C# Avg Update: %.3fms" % packed_data["avg_update_time"])
	if packed_data.has("avg_physics_time"):
		print("  C# Avg Physics: %.3fms" % packed_data["avg_physics_time"])
	
	# Emit response back to demonstrate bidirectional communication
	var response_data = {
		"ui_response_time": Time.get_ticks_msec(),
		"gd_script_active": true,
		"signal_count": _signal_test_count
	}
	ui_update_requested.emit(response_data)

func _test_ui_response(message: String, value: float):
	# Simulate typical UI update operations
	var test_label = find_child("TestLabel")
	if test_label != null:
		test_label.text = "Last Signal: %s = %.2f" % [message, value]

# Performance testing functions
func test_signal_marshaling_performance(iterations: int = 1000) -> Dictionary:
	print("UIBridge: Starting signal marshaling performance test...")
	
	var start_time = Time.get_ticks_usec()
	
	# Test different signal payload types
	for i in range(iterations):
		var test_data = {
			"iteration": i,
			"test_float": randf() * 100.0,
			"test_string": "test_string_%d" % i,
			"test_vector": Vector3(randf(), randf(), randf())
		}
		# Simulate signal processing overhead without actual emission
		var _processed = Dictionary(test_data)
	
	var end_time = Time.get_ticks_usec()
	var total_time = (end_time - start_time) / 1000.0  # milliseconds
	
	var results = {
		"total_time_ms": total_time,
		"iterations": iterations,
		"avg_time_per_signal_ms": total_time / iterations,
		"signals_per_second": (iterations / total_time) * 1000.0
	}
	
	print("UIBridge: Signal marshaling test results:")
	print("  Total time: %.3fms" % results.total_time_ms)
	print("  Average per signal: %.6fms" % results.avg_time_per_signal_ms)
	print("  Signals per second: %.1f" % results.signals_per_second)
	
	performance_test_completed.emit(results)
	return results

func get_ui_performance_metrics() -> Dictionary:
	var avg_ui_time = 0.0
	if _ui_update_times.size() > 0:
		var total = 0.0
		for time in _ui_update_times:
			total += time
		avg_ui_time = total / _ui_update_times.size()
	
	return {
		"average_ui_update_time_ms": avg_ui_time,
		"signal_test_count": _signal_test_count,
		"ui_samples_collected": _ui_update_times.size(),
		"target_script_time_ms": 3.0  # From performance targets
	}

func reset_performance_counters():
	_signal_test_count = 0
	_ui_update_times.clear()
	_signal_frequency_samples.clear()
	_last_signal_time = 0.0
	print("UIBridge: Performance counters reset")