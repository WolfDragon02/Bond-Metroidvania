extends Control
func ready():
	$AnimationPlayer.play("RESET")

func resume(): 
	get_tree().paused = false
	$AnimationPlayer.play_backwards("blur") 

func pause():
	get_tree().paused = true
	$AnimationPlayer.play("blur")

func testescape():
	if Input.is_action_just_pressed("escapes") and get_tree().paused == false:
		pause()
	elif Input.is_action_just_pressed("escapes") and get_tree().paused == true:
		resume()


func _on_resume_pressed() -> void:
	resume()


func _on_restart_pressed() -> void:
	resume()
	get_tree().reload_current_scene()


func _on_return_to_main_menu_pressed() -> void:
	get_tree().paused = false
	get_tree().change_scene_to_file("res://MainMenu/Main_Menu.tscn")


func _on_quit_game_pressed() -> void:
	get_tree().quit()

func _process(delta):
	testescape()
