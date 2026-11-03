using Godot;
using System;

public partial class Main_Menu : Control
{
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
