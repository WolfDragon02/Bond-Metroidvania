using Godot;
using System;

// Walks back and forth, turning around at walls and ledges.
public partial class Crawler : Enemy
{
	[Export] public RayCast2D LedgeCheck;

	private int _direction = -1;

	public override void _Ready()
	{
		ApplyDirection();
	}

	protected override Vector2 Move(Vector2 velocity, double delta)
	{
		if (IsOnFloor())
		{
			bool hitWall = IsOnWall();
			bool atLedge = LedgeCheck != null && !LedgeCheck.IsColliding();
			if (hitWall || atLedge)
			{
				_direction = -_direction;
				ApplyDirection();
			}
		}

		velocity.X = _direction * Speed;
		return velocity;
	}

	// Keep the ledge ray on the side the crawler is walking toward.
	private void ApplyDirection()
	{
		if (LedgeCheck != null)
		{
			LedgeCheck.Position = new Vector2(Math.Abs(LedgeCheck.Position.X) * _direction, LedgeCheck.Position.Y);
			LedgeCheck.ForceRaycastUpdate();
		}
	}
}
