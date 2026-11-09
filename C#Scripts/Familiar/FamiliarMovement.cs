using Godot;
using System;

public partial class FamiliarMovement : Node
{
	[Export] public Node2D Player { get; set; }

	[Export] public float FollowSpeed;

	[Export] public float FollowDistance = 30f;

	[Export] public float StopDistance = 4f;

	[Export] public float FloatHeight = 5f;

	[Export] public float FloatSpeed = 3f;

	private AnimatedSprite2D _animatedSprite;
	private float _time = 0f;
	
	public override void _Ready() {
		_animatedSprite = GetParent()
			.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		
		_animatedSprite.Play("default");
		
		Player = GetTree()
			.GetFirstNodeInGroup("player") as Node2D;
	if (Player == null)
		{
			GD.PrintErr("FAMILIAR: Player not found!");
		}
		else
		{
			GD.Print("FAMILIAR: Player detected!");
		}
	}
	public void FamMovement(Entity familiar, double delta) {
		if (Player == null)
			return;
		float dt = (float)delta;
		_time += dt;
		
		Vector2 offset = new Vector2(
			FollowDistance,
			-20f
		);
		
		Vector2 targetPosition = 
			Player.GlobalPosition + offset;
		
		Vector2 direction =
			targetPosition - familiar.GlobalPosition;
		
		if (direction.Length() > StopDistance){
			float moveAmount = Mathf.Min(
					FollowSpeed * dt,
					direction.Length() - StopDistance
			);
			
			familiar.GlobalPosition +=
				direction.Normalized() * moveAmount;
		}
		
		float floatOffset = 
			Mathf.Sin(_time * FloatSpeed)
			* FloatHeight;
		_animatedSprite.Position = new Vector2(
			0,
			floatOffset
		);
	}
}
