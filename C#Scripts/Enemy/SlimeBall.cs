using Godot;
using System;

// A glob of slime the Slime King spits. It flies in an arc, hurts the
// player on contact and splats when it hits the player or the ground.
public partial class SlimeBall : Node2D
{
	[Export]
	public int Damage { get; set; } = 10;

	[Export]
	public float FallAcceleration { get; set; } = 980f;

	[Export]
	public float Radius { get; set; } = 9f;

	// Removed after this long in case it flies off the level.
	[Export]
	public double LifeSeconds { get; set; } = 5.0;

	public Vector2 Velocity { get; set; }

	private AnimatedSprite2D _sprite;
	private PhysicsShapeQueryParameters2D _query;
	private string _suffix = "";
	private bool _popped;
	private double _age;

	// Called right after the glob is added to the level.
	public void Launch(Vector2 velocity, bool enraged)
	{
		Velocity = velocity;
		_suffix = enraged ? "_rage" : "";
		_sprite ??= GetNode<AnimatedSprite2D>("Sprite");
		_sprite.Play("fly" + _suffix);
	}

	public override void _Ready()
	{
		_sprite ??= GetNode<AnimatedSprite2D>("Sprite");
		_sprite.AnimationFinished += () =>
		{
			if (_popped)
				QueueFree();
		};

		// Checks for walls, floors and the player (collision layer 1).
		_query = new PhysicsShapeQueryParameters2D
		{
			Shape = new CircleShape2D { Radius = Radius },
			CollisionMask = 1,
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_popped)
			return;

		_age += delta;
		if (_age > LifeSeconds)
		{
			QueueFree();
			return;
		}

		Velocity += new Vector2(0, FallAcceleration * (float)delta);
		Position += Velocity * (float)delta;
		CheckHits();
	}

	// Splats on the first thing it touches, hurting the player if it's them.
	private void CheckHits()
	{
		_query.Transform = GlobalTransform;
		var hits = GetWorld2D().DirectSpaceState.IntersectShape(_query, 8);
		foreach (var hit in hits)
		{
			var body = hit["collider"].As<Node>();

			// Globs fly through enemies, including the boss that threw them.
			if (body == null || body is EnemyController)
				continue;

			if (body.IsInGroup("player") && body is Entity player)
			{
				player.TakeDamage(Damage);
			}
			else
			{
				SitOnSurface();
			}

			Pop();
			return;
		}
	}

	// When it lands on a floor, move the splat up onto the surface so it
	// doesn't sink into the ground.
	private void SitOnSurface()
	{
		var rest = GetWorld2D().DirectSpaceState.GetRestInfo(_query);
		if (rest.Count == 0)
			return;

		Vector2 normal = rest["normal"].AsVector2();
		if (normal.Y < -0.5f)
		{
			Vector2 point = rest["point"].AsVector2();
			GlobalPosition = new Vector2(GlobalPosition.X, point.Y - 9f);
		}
	}

	private void Pop()
	{
		_popped = true;
		Velocity = Vector2.Zero;
		_sprite.Play("splat" + _suffix);
	}
}
