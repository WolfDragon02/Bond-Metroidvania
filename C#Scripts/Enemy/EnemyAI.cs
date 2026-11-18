using Godot;
using System;

// Base class for optional enemy behaviors that react to the player.
// An AI usually only steers EnemyMovement; EnemyMovement still moves the enemy.
public abstract partial class EnemyAI : Node
{
	// True for AIs that move the enemy themselves (e.g. a jumping boss).
	// EnemyController then skips EnemyMovement and the hit freeze.
	public virtual bool ControlsMovement => false;

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
