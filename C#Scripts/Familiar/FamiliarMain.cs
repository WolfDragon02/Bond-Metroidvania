using Godot;
using System;

public partial class FamiliarMain : Entity
{
	private FamiliarMovement _fmovement = null!;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		base._Ready();
		_fmovement = GetNode<FamiliarMovement>("FamiliarMovement");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_fmovement.FamMovement(this, delta);
	}
}
