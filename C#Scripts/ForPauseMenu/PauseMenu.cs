using Godot;
using System;

public partial class PauseMenu : Control
{
	private AnimationPlayer animationPlayer;
	
	public override void _Ready() {
		animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		animationPlayer.Play("RESET");
	}
	
	private void Resume() {
		GetTree().Paused = false;
		animationPlayer.PlayBackwards("blur");
	}
	private void Pause() {
		GetTree().Paused = true;
		animationPlayer.Play("blur");
	}
	private void TestEscape() {
		if (Input.IsActionJustPressed("escapes") && !GetTree().Paused) {
			Pause();
		}
		else if (Input.IsActionJustPressed("escapes") && GetTree().Paused) {
			Resume();
		}
	}
	private void OnResumedPressed() {
		Resume();
	}
	private void OnRestartPressed() {
		Resume();
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}
	private void OnReturnToMainMenu() {
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://MainMenu/Main_Menu.tscn");
	}
	private void OnQuitGame() {
		GetTree().Quit();
	}
	public override void _Process(double delta) {
		TestEscape();
	}
}
