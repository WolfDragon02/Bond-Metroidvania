using Godot;
using System;

// Patrols like the Crawler until the player gets close, flashes as a
// warning, then rushes at the player and needs a moment to recover.
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

	private enum State { Patrol, Telegraph, Charge, Recover }

	private State _state = State.Patrol;
	private double _timer;

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
					// Face the player and paw the ground while flashing.
					movement.Direction =
						player.GlobalPosition.X < enemy.GlobalPosition.X ? -1 : 1;
					movement.SpeedMultiplier = 0f;
					movement.TurnAtEdges = false;
					enemy.AnimationOverride = "windup";
					Enter(State.Telegraph, TelegraphSeconds);
				}
				break;

			case State.Telegraph:
				// Blink every 0.1 seconds as a warning.
				bool bright = (int)(_timer / 0.1) % 2 == 0;
				enemy.SetFlash(bright ? 0.6f : 0f);

				if (_timer <= 0)
				{
					enemy.SetFlash(0f);
					movement.SpeedMultiplier = ChargeSpeedMultiplier;
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
					EndCharge(enemy, movement, hitWall ? "stunned" : "idle",
						hitWall ? StunSeconds : RecoverSeconds);
				}
				break;

			case State.Recover:
				if (_timer <= 0)
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

	private bool PlayerInRange(EnemyController enemy, Node2D player)
	{
		Vector2 offset = player.GlobalPosition - enemy.GlobalPosition;
		return Math.Abs(offset.X) <= DetectionRange
			&& Math.Abs(offset.Y) <= DetectionHeight;
	}

	private void Enter(State state, double seconds)
	{
		_state = state;
		_timer = seconds;
	}
}
