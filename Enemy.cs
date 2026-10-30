using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed = 80.0f;
	[Export] public int Health = 3;
	[Export] public int ContactDamage = 1;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		velocity = Move(velocity, delta);

		Velocity = velocity;
		MoveAndSlide();
	}

	// Subclasses override this to set horizontal (and vertical) movement.
	protected virtual Vector2 Move(Vector2 velocity, double delta)
	{
		return velocity;
	}

	public virtual void TakeDamage(int amount)
	{
		Health -= amount;
		if (Health <= 0)
		{
			QueueFree();
		}
	}
}
