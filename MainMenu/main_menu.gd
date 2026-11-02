extends Control


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass


func _on_start_pressed() -> void:
	get_tree().change_scene_to_file("res://PlaceholderScene/PlaceHolder.tscn")
	print("Start Pressed")


func _on_options_pressed() -> void:
	print("Options Pressed")



func _on_quit_game_pressed() -> void:
	get_tree().quit()
	print("Quit Pressed")
