using Godot;
using System;

public partial class Main_Menu : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void OnStartPressed() {
		GetTree().ChangeSceneToFile("res://level01.tscn");
		GD.Print("Start Pressed");
	}
	private void OnOptionsPressed() {
		GD.Print("Options Pressed");
	}
	private void OnQuitGame() {
		GD.Print("Quit Pressed");
		GetTree().Quit();
	}
}
