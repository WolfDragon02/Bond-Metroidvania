using Godot;
using System;

// Handles enemy movement only: gravity and walking back and forth,
// turning around at walls and ledges.
public partial class EnemyMovement : Node
{
	// Controls how quickly the enemy falls.
	[Export]
	public float Gravity = 980f;

	// -1 walks left, 1 walks right.
	private int _direction = -1;

	private RayCast2D _ledgeCheck = null!;

	public override void _Ready()
	{
		// The ledge ray is a child of the Enemy node.
		_ledgeCheck = GetParent().GetNode<RayCast2D>("LedgeCheck");
		ApplyDirection();
	}

	// Called by EnemyController every physics frame.
	public void UpdateMovement(Entity enemy, double delta)
	{
		Vector2 velocity = enemy.Velocity;

		// Apply gravity when the enemy is in the air.
		if (!enemy.IsOnFloor())
		{
			velocity.Y += Gravity * (float)delta;
		}
		else if (enemy.IsOnWall() || !_ledgeCheck.IsColliding())
		{
			// Turn around at a wall or the edge of a platform.
			_direction = -_direction;
			ApplyDirection();
		}

		// Use MovementSpeed from the Entity class.
		velocity.X = _direction * enemy.MovementSpeed;

		enemy.Velocity = velocity;
		enemy.MoveAndSlide();
	}

	// Keep the ledge ray on the side the enemy is walking toward.
	private void ApplyDirection()
	{
		_ledgeCheck.Position = new Vector2(
			Math.Abs(_ledgeCheck.Position.X) * _direction,
			_ledgeCheck.Position.Y);
		_ledgeCheck.ForceRaycastUpdate();
	}
}
