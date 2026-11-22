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

	// True when no wall or floor is between the enemy and the player.
	// Characters in the way (other enemies, the familiar) don't block it.
	// eyeHeight lifts the ray off the ground so low bumps don't block it.
	protected bool CanSee(CollisionObject2D enemy, Node2D player, float eyeHeight)
	{
		var exclude = new Godot.Collections.Array<Rid> { enemy.GetRid() };
		if (player is CollisionObject2D playerBody)
		{
			exclude.Add(playerBody.GetRid());
		}

		var space = enemy.GetWorld2D().DirectSpaceState;
		for (int i = 0; i < 4; i++)
		{
			var query = PhysicsRayQueryParameters2D.Create(
				enemy.GlobalPosition + new Vector2(0, -eyeHeight),
				player.GlobalPosition);
			query.Exclude = exclude;

			var hit = space.IntersectRay(query);
			if (hit.Count == 0)
				return true;

			if (hit["collider"].As<Node>() is not CharacterBody2D)
				return false;

			// Look past the character and try again.
			exclude.Add(hit["rid"].AsRid());
		}

		return true;
	}
}
