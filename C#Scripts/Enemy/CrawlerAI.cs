using Godot;
using System;

// Patrols until it sees the player, hops up with a "!", then scuttles
// after them. When it gets close it crouches and pounces. It stops at
// ledges instead of chasing off them, and gives up when the player gets
// far away or stays out of sight.
public partial class CrawlerAI : EnemyAI
{
	// How far ahead or behind the Crawler notices the player.
	[Export]
	public float SightRange { get; set; } = 240f;

	// Ignore the player when they are this far above or below.
	[Export]
	public float SightHeight { get; set; } = 50f;

	// Stops chasing when the player gets this far away.
	[Export]
	public float LoseRange { get; set; } = 380f;

	// Stops chasing after the player has been out of sight this long.
	[Export]
	public double ForgetSeconds { get; set; } = 2.0;

	// The "!" hop when it first spots the player.
	[Export]
	public double AlertSeconds { get; set; } = 0.45;

	// Chase speed as a multiple of MovementSpeed.
	[Export]
	public float ChaseSpeedMultiplier { get; set; } = 2.2f;

	// Pounces when the player is this close.
	[Export]
	public float PounceRange { get; set; } = 120f;

	// Crouch before the pounce; this is the player's warning.
	[Export]
	public double PounceWindupSeconds { get; set; } = 0.35;

	[Export]
	public float PounceHeight { get; set; } = 60f;

	[Export]
	public float MaxPounceSpeed { get; set; } = 260f;

	// Time between pounces.
	[Export]
	public double PounceCooldown { get; set; } = 1.2;

	// Pause after landing a pounce.
	[Export]
	public double RecoverSeconds { get; set; } = 0.45;

	private enum State { Patrol, Alert, Chase, Windup, Pounce, Recover }

	private State _state = State.Patrol;
	private double _timer;
	private double _cooldown;
	private double _unseenTime;
	private bool _leftGround;

	public override void UpdateAI(
		EnemyController enemy, EnemyMovement movement, double delta)
	{
		_timer -= delta;
		_cooldown -= delta;
		Node2D player = GetPlayer();

		switch (_state)
		{
			case State.Patrol:
				if (player != null && Spots(enemy, player))
				{
					Alert(enemy, movement, player);
				}
				break;

			case State.Alert:
				if (_timer <= 0)
				{
					StartChase(enemy, movement);
				}
				break;

			case State.Chase:
				Chase(enemy, movement, player, delta);
				break;

			case State.Windup:
				// Blink as a warning.
				enemy.SetFlash((int)(_timer / 0.08) % 2 == 0 ? 0.5f : 0f);
				if (_timer <= 0)
				{
					enemy.SetFlash(0f);
					if (player != null && InPounceRange(enemy, player))
					{
						// Re-aim in case the player moved during the crouch.
						FacePlayer(enemy, movement, player);
						Pounce(enemy, movement, player);
					}
					else
					{
						// The player got away; go back to chasing.
						StartChase(enemy, movement);
					}
				}
				break;

			case State.Pounce:
				if (!enemy.IsOnFloor())
				{
					_leftGround = true;
				}
				else if (_leftGround)
				{
					movement.SpeedMultiplier = 0f;
					enemy.AnimationOverride = "windup";
					_cooldown = PounceCooldown;
					Enter(State.Recover, RecoverSeconds);
				}
				break;

			case State.Recover:
				if (_timer <= 0)
				{
					StartChase(enemy, movement);
				}
				break;
		}
	}

	public override void OnHurt(EnemyController enemy, EnemyMovement movement)
	{
		Node2D player = GetPlayer();
		if (player == null)
			return;

		// Getting hit makes it chase right away, even from behind.
		if (_state == State.Patrol)
		{
			FacePlayer(enemy, movement, player);
			StartChase(enemy, movement);
		}
		else if (_state == State.Windup)
		{
			// A hit during the crouch cancels the pounce.
			enemy.SetFlash(0f);
			_cooldown = PounceCooldown;
			StartChase(enemy, movement);
		}
	}

	private void Chase(EnemyController enemy, EnemyMovement movement, Node2D player, double delta)
	{
		if (player == null)
		{
			StopChase(enemy, movement);
			return;
		}

		Vector2 offset = player.GlobalPosition - enemy.GlobalPosition;
		_unseenTime = CanSee(enemy, player, 14f) ? 0 : _unseenTime + delta;
		if (Math.Abs(offset.X) > LoseRange || Math.Abs(offset.Y) > 160f
			|| _unseenTime > ForgetSeconds)
		{
			StopChase(enemy, movement);
			return;
		}

		// A small dead zone stops it flipping back and forth under the player.
		if (Math.Abs(offset.X) > 10f)
		{
			FacePlayer(enemy, movement, player);
		}

		bool blocked = movement.IsBlockedAhead(enemy);
		movement.SpeedMultiplier = blocked ? 0f : ChaseSpeedMultiplier;
		// At a ledge or wall it waits and watches instead of running off.
		enemy.AnimationOverride = blocked ? "alert" : "run";

		if (!blocked && _cooldown <= 0 && enemy.IsOnFloor()
			&& InPounceRange(enemy, player))
		{
			movement.SpeedMultiplier = 0f;
			enemy.AnimationOverride = "windup";
			Enter(State.Windup, PounceWindupSeconds);
		}
	}

	// Jump at the player, timed to land where they are standing.
	private void Pounce(EnemyController enemy, EnemyMovement movement, Node2D player)
	{
		float distance = player != null
			? Math.Abs(player.GlobalPosition.X - enemy.GlobalPosition.X)
			: PounceRange;
		float upSpeed = (float)Math.Sqrt(2 * movement.Gravity * PounceHeight);
		float airSeconds = 2 * upSpeed / movement.Gravity;
		float speed = Math.Clamp(distance / airSeconds, 60f, MaxPounceSpeed);

		movement.SpeedMultiplier = speed / enemy.MovementSpeed;
		enemy.Velocity = new Vector2(movement.Direction * speed, -upSpeed);
		enemy.AnimationOverride = "pounce";
		_leftGround = false;
		Enter(State.Pounce, 0);
	}

	private void Alert(EnemyController enemy, EnemyMovement movement, Node2D player)
	{
		FacePlayer(enemy, movement, player);
		movement.SpeedMultiplier = 0f;
		movement.TurnAtEdges = false;
		enemy.AnimationOverride = "alert";
		if (enemy.IsOnFloor())
		{
			// A little startled hop.
			enemy.Velocity = new Vector2(0, -220f);
		}
		Enter(State.Alert, AlertSeconds);
	}

	private void StartChase(EnemyController enemy, EnemyMovement movement)
	{
		movement.TurnAtEdges = false;
		movement.SpeedMultiplier = ChaseSpeedMultiplier;
		enemy.AnimationOverride = "run";
		_unseenTime = 0;
		Enter(State.Chase, 0);
	}

	private void StopChase(EnemyController enemy, EnemyMovement movement)
	{
		movement.SpeedMultiplier = 1f;
		movement.TurnAtEdges = true;
		enemy.AnimationOverride = null;
		Enter(State.Patrol, 0);
	}

	private bool Spots(EnemyController enemy, Node2D player)
	{
		Vector2 offset = player.GlobalPosition - enemy.GlobalPosition;
		return Math.Abs(offset.X) <= SightRange
			&& Math.Abs(offset.Y) <= SightHeight
			&& CanSee(enemy, player, 14f);
	}

	// Close enough to pounce, and at about the same height.
	private bool InPounceRange(EnemyController enemy, Node2D player)
	{
		Vector2 offset = player.GlobalPosition - enemy.GlobalPosition;
		return Math.Abs(offset.X) <= PounceRange && Math.Abs(offset.Y) <= 40f;
	}

	private static void FacePlayer(EnemyController enemy, EnemyMovement movement, Node2D player)
	{
		movement.Direction = player.GlobalPosition.X < enemy.GlobalPosition.X ? -1 : 1;
	}

	private void Enter(State state, double seconds)
	{
		_state = state;
		_timer = seconds;
	}
}
