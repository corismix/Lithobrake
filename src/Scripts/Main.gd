extends Node3D

## Main scene controller for Lithobrake rocket simulation
## Handles scene initialization and basic UI

var info_label: Label

func _ready():
	print("Lithobrake: Main scene loaded")
	
	# Find UI components
	info_label = $UI/InfoLabel
	
	# Initialize core systems
	initialize_systems()

func _input(event):
	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_F1:
				show_help()
			KEY_ESCAPE:
				get_tree().quit()

func initialize_systems():
	"""Initialize core Lithobrake systems"""
	print("Initializing core systems...")
	
	# Core systems will be initialized automatically via autoload
	# or singleton patterns as implemented in the C# classes

func show_help():
	"""Display help information"""
	var help_text = """
Lithobrake - Rocket Simulation Game

Controls:
F1 - Show this help
ESC - Quit game

Note: This is the main scene entry point.
Camera system and full gameplay will be implemented in subsequent tasks.
"""
	print(help_text)
	
	# Update UI label temporarily
	if info_label:
		info_label.text = help_text
		# Reset after 5 seconds
		get_tree().create_timer(5.0).timeout.connect(_reset_info_label)

func _reset_info_label():
	"""Reset info label to default text"""
	if info_label:
		info_label.text = "Lithobrake - Rocket Simulation\nPress F1 for help"