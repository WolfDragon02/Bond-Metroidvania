using Godot;
using System;

// Handles enemy movement only: gravity and walking back and forth,
// turning around at walls and ledges.
public partial class EnemyMovement : Node
{
	// Controls how quickly the enemy falls.
	[Export]
	public float Gravity = 980f;

	// Scales MovementSpeed. An EnemyAI sets 0 to stand still
	// or a larger value to charge.
	public float SpeedMultiplier { get; set; } = 1f;

	// When false the enemy no longer turns around at walls and
	// ledges, so an EnemyAI can decide what to do there.
	public bool TurnAtEdges { get; set; } = true;

	// -1 walks left, 1 walks right.
	public int Direction
	{
		get => _direction;
		set
		{
			_direction = value < 0 ? -1 : 1;
			ApplyDirection();
		}
	}

	private int _direction = -1;

	private RayCast2D _ledgeCheck = null!;

	public override void _Ready()
	{
		// The ledge ray is a child of the Enemy node.
		_ledgeCheck = GetParent().GetNode<RayCast2D>("LedgeCheck");
		ApplyDirection();
	}

	// Called by EnemyController every physics frame. canWalk = false
	// keeps gravity but stops walking (e.g. while the enemy is hurt).
	public void UpdateMovement(Entity enemy, double delta, bool canWalk = true)
	{
		Vector2 velocity = enemy.Velocity;

		// Apply gravity when the enemy is in the air.
		if (!enemy.IsOnFloor())
		{
			velocity.Y += Gravity * (float)delta;
		}
		else if (canWalk && TurnAtEdges && IsBlockedAhead(enemy))
		{
			// Turn around at a wall or the edge of a platform.
			Direction = -_direction;
		}

		// Use MovementSpeed from the Entity class.
		velocity.X = canWalk
			? _direction * enemy.MovementSpeed * SpeedMultiplier
			: 0f;

		enemy.Velocity = velocity;
		enemy.MoveAndSlide();
	}

	// True when the enemy is on the floor and facing a wall
	// or the edge of a platform.
	public bool IsBlockedAhead(Entity enemy)
	{
		return enemy.IsOnFloor()
			&& (enemy.IsOnWall() || !_ledgeCheck.IsColliding());
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
