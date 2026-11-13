using Godot;
using System;
using System.Linq;

// Coordinates the enemy's separate modules.
public partial class EnemyController : Entity
{
	// Damage dealt to the player on contact (used by later combat code).
	[Export]
	public int ContactDamage { get; set; } = 1;

	private EnemyMovement _movement = null!;

	// Optional behavior, e.g. ChargerAI. The Crawler has none.
	private EnemyAI _ai;

	// Optional visual that flips to face the walking direction.
	private Node2D _body;

	public override void _Ready()
	{
		// Initialize shared Entity functionality.
		base._Ready();

		_movement = GetNode<EnemyMovement>("EnemyMovement");
		_ai = GetChildren().OfType<EnemyAI>().FirstOrDefault();
		_body = GetNodeOrNull<Node2D>("Body");

		// Remove the enemy when its HealthComponent reports death.
		var health = GetNode<HealthComponent>("HealthComponent");
		health.Died += OnDied;
	}

	// Let the AI steer, then update movement every physics frame.
	public override void _PhysicsProcess(double delta)
	{
		_ai?.UpdateAI(this, _movement, delta);
		_movement.UpdateMovement(this, delta);

		if (_body != null)
		{
			_body.Scale = new Vector2(_movement.Direction, 1);
		}
	}

	private void OnDied()
	{
		QueueFree();
	}
}
