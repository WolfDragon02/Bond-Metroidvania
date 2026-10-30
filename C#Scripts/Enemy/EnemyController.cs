using Godot;
using System;

// Coordinates the enemy's separate modules.
public partial class EnemyController : Entity
{
	// Damage dealt to the player on contact (used by later combat code).
	[Export]
	public int ContactDamage { get; set; } = 1;

	private EnemyMovement _movement = null!;

	public override void _Ready()
	{
		// Initialize shared Entity functionality.
		base._Ready();

		_movement = GetNode<EnemyMovement>("EnemyMovement");

		// Remove the enemy when its HealthComponent reports death.
		var health = GetNode<HealthComponent>("HealthComponent");
		health.Died += OnDied;
	}

	// Update movement every physics frame.
	public override void _PhysicsProcess(double delta)
	{
		_movement.UpdateMovement(this, delta);
	}

	private void OnDied()
	{
		QueueFree();
	}
}
