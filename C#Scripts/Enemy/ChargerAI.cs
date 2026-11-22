using Godot;
using System;

// Patrols until it sees the player, flashes as a warning, then rushes
// at the player and needs a moment to recover. If the charge misses and
// the player is behind it, it skids, turns and charges once more.
// Below half health it gets angry: shorter warning, faster charges.
public partial class ChargerAI : EnemyAI
{
	// How far ahead or behind the Charger notices the player.
	[Export]
	public float DetectionRange { get; set; } = 260f;

	// Ignore the player when they are this far above or below.
	[Export]
	public float DetectionHeight { get; set; } = 60f;

	// How long the Charger flashes before it charges.
	[Export]
	public double TelegraphSeconds { get; set; } = 0.5;

	// Charge speed as a multiple of MovementSpeed.
	[Export]
	public float ChargeSpeedMultiplier { get; set; } = 3f;

	[Export]
	public double ChargeSeconds { get; set; } = 1.2;

	// How long the Charger stands still after a charge.
	[Export]
	public double RecoverSeconds { get; set; } = 1.0;

	// How long the Charger stays dazed after slamming into a wall.
	[Export]
	public double StunSeconds { get; set; } = 1.8;

	// The skid before turning around for a second charge.
	[Export]
	public double TurnSeconds { get; set; } = 0.35;

	// Below half health: the warning is this much shorter and charges
	// this much faster.
	[Export]
	public float AngryTelegraphScale { get; set; } = 0.6f;

	[Export]
	public float AngrySpeedScale { get; set; } = 1.25f;

	// Reddish tint while angry.
	[Export]
	public Color AngryTint { get; set; } = new(1f, 0.72f, 0.66f);

	private enum State { Patrol, Telegraph, Charge, Recover }

	private State _state = State.Patrol;
	private double _timer;
	private bool _turnBack;
	private bool _isSecondCharge;
	private bool _angry;

	// Optional dust trail kicked up while charging.
	private CpuParticles2D _dust;

	public override void UpdateAI(
		EnemyController enemy, EnemyMovement movement, double delta)
	{
		_dust ??= enemy.GetNodeOrNull<CpuParticles2D>("Dust");
		_timer -= delta;

		switch (_state)
		{
			case State.Patrol:
				Node2D player = GetPlayer();
				if (player != null && PlayerInRange(enemy, player))
				{
					_isSecondCharge = false;
					StartTelegraph(enemy, movement, player, TelegraphSeconds);
				}
				break;

			case State.Telegraph:
				// Blink every 0.1 seconds as a warning.
				bool bright = (int)(_timer / 0.1) % 2 == 0;
				enemy.SetFlash(bright ? 0.6f : 0f);

				if (_timer <= 0)
				{
					enemy.SetFlash(0f);
					movement.SpeedMultiplier = ChargeSpeedMultiplier * (_angry ? AngrySpeedScale : 1f);
					enemy.AnimationOverride = "charge";
					SetDust(movement.Direction, true);
					Enter(State.Charge, ChargeSeconds);
				}
				break;

			case State.Charge:
				// Stop at a wall or ledge instead of turning around mid-charge.
				if (_timer <= 0 || movement.IsBlockedAhead(enemy))
				{
					// Slamming into a wall leaves the Charger dazed for longer.
					bool hitWall = enemy.IsOnWall();
					if (!hitWall && !_isSecondCharge && PlayerBehind(enemy, movement))
					{
						// Missed: skid, then turn and charge back once.
						_turnBack = true;
						EndCharge(enemy, movement, "windup", TurnSeconds);
					}
					else
					{
						EndCharge(enemy, movement, hitWall ? "stunned" : "idle",
							hitWall ? StunSeconds : RecoverSeconds);
					}
				}
				break;

			case State.Recover:
				if (_timer <= 0 && _turnBack)
				{
					_turnBack = false;
					_isSecondCharge = true;
					StartTelegraph(enemy, movement, GetPlayer(), TelegraphSeconds * 0.5);
				}
				else if (_timer <= 0)
				{
					movement.SpeedMultiplier = 1f;
					movement.TurnAtEdges = true;
					enemy.AnimationOverride = null;
					Enter(State.Patrol, 0);
				}
				break;
		}
	}

	public override void OnHurt(EnemyController enemy, EnemyMovement movement)
	{
		var health = enemy.GetNode<HealthComponent>("HealthComponent");
		if (!_angry && !health.IsDead && health.CurrentHealth <= health.MaxHealth / 2)
		{
			_angry = true;
			var sprite = enemy.GetNodeOrNull<CanvasItem>("Sprite");
			if (sprite != null)
				sprite.SelfModulate = AngryTint;
		}

		_turnBack = false;

		// Getting hit cancels the wind-up or the charge.
		if (_state == State.Telegraph || _state == State.Charge)
		{
			EndCharge(enemy, movement, "idle", RecoverSeconds);
		}
	}

	private void EndCharge(
		EnemyController enemy, EnemyMovement movement,
		string animation, double seconds)
	{
		enemy.SetFlash(0f);
		movement.SpeedMultiplier = 0f;
		enemy.AnimationOverride = animation;
		SetDust(movement.Direction, false);
		Enter(State.Recover, seconds);
	}

	// Turn the dust on or off, kicking it out behind the Charger.
	private void SetDust(int direction, bool on)
	{
		if (_dust == null)
			return;

		_dust.Position = new Vector2(
			-Math.Abs(_dust.Position.X) * direction, _dust.Position.Y);
		_dust.Direction = new Vector2(-direction, _dust.Direction.Y);
		_dust.Emitting = on;
	}

	// Face the player and paw the ground while flashing.
	private void StartTelegraph(
		EnemyController enemy, EnemyMovement movement, Node2D player, double seconds)
	{
		if (player != null)
		{
			movement.Direction =
				player.GlobalPosition.X < enemy.GlobalPosition.X ? -1 : 1;
		}
		movement.SpeedMultiplier = 0f;
		movement.TurnAtEdges = false;
		enemy.AnimationOverride = "windup";
		Enter(State.Telegraph, _angry ? seconds * AngryTelegraphScale : seconds);
	}

	// The player is in range and visible, but on the other side.
	private bool PlayerBehind(EnemyController enemy, EnemyMovement movement)
	{
		Node2D player = GetPlayer();
		if (player == null || !PlayerInRange(enemy, player))
			return false;

		float dx = player.GlobalPosition.X - enemy.GlobalPosition.X;
		return Math.Sign(dx) != movement.Direction;
	}

	// Close enough, at about the same height, and not behind a wall.
	private bool PlayerInRange(EnemyController enemy, Node2D player)
	{
		Vector2 offset = player.GlobalPosition - enemy.GlobalPosition;
		return Math.Abs(offset.X) <= DetectionRange
			&& Math.Abs(offset.Y) <= DetectionHeight
			&& CanSee(enemy, player, 24f);
	}

	private void Enter(State state, double seconds)
	{
		_state = state;
		_timer = seconds;
	}
}
