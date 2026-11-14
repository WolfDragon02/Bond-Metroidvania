using Godot;
using System;

// Base class for optional enemy behaviors that react to the player.
// An AI only steers EnemyMovement; EnemyMovement still moves the enemy.
public abstract partial class EnemyAI : Node
{
	// Called by EnemyController every physics frame, before movement.
	public abstract void UpdateAI(
		EnemyController enemy, EnemyMovement movement, double delta);

	// Called by EnemyController when the enemy takes damage.
	public virtual void OnHurt(EnemyController enemy, EnemyMovement movement)
	{
	}

	private Node2D _player;

	// Finds the player through the "player" group set in Player.tscn.
	protected Node2D GetPlayer()
	{
		if (!IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		}

		return _player;
	}
}
