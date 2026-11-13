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

	// Modulate values above 1 brighten the body while it flashes.
	private static readonly Color FlashColor = new Color(2f, 2f, 2f);

	private enum State { Patrol, Telegraph, Charge, Recover }

	private State _state = State.Patrol;
	private double _timer;

	public override void UpdateAI(
		EnemyController enemy, EnemyMovement movement, double delta)
	{
		_timer -= delta;

		switch (_state)
		{
			case State.Patrol:
				Node2D player = GetPlayer();
				if (player != null && PlayerInRange(enemy, player))
				{
					// Face the player and stand still while flashing.
					movement.Direction =
						player.GlobalPosition.X < enemy.GlobalPosition.X ? -1 : 1;
					movement.SpeedMultiplier = 0f;
					movement.TurnAtEdges = false;
					Enter(State.Telegraph, TelegraphSeconds);
				}
				break;

			case State.Telegraph:
				// Blink between bright and normal every 0.1 seconds.
				bool bright = (int)(_timer / 0.1) % 2 == 0;
				enemy.Modulate = bright ? FlashColor : Colors.White;

				if (_timer <= 0)
				{
					enemy.Modulate = Colors.White;
					movement.SpeedMultiplier = ChargeSpeedMultiplier;
					Enter(State.Charge, ChargeSeconds);
				}
				break;

			case State.Charge:
				// Stop at a wall or ledge instead of turning around mid-charge.
				if (_timer <= 0 || movement.IsBlockedAhead(enemy))
				{
					movement.SpeedMultiplier = 0f;
					Enter(State.Recover, RecoverSeconds);
				}
				break;

			case State.Recover:
				if (_timer <= 0)
				{
					movement.SpeedMultiplier = 1f;
					movement.TurnAtEdges = true;
					Enter(State.Patrol, 0);
				}
				break;
		}
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
