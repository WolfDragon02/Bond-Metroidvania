using Godot;
using System;

// The Giant Slime King boss.
// Phase 1 (above half health): walks toward the player, jumps, hangs in
// the air for a moment above where it will land, slams down with a
// damaging shockwave, then rests so the player can attack.
// Phase 2 (half health or less): enraged. Moves faster, jumps farther
// and rests for less time.
public partial class SlimeKingAI : EnemyAI
{
	// The fight starts when the player comes this close.
	[Export]
	public float WakeRange { get; set; } = 500f;

	// Longest the boss walks before it jumps anyway.
	[Export]
	public double ApproachSeconds { get; set; } = 1.4;

	// The boss jumps as soon as the player is this close.
	[Export]
	public float JumpTriggerRange { get; set; } = 260f;

	// Squash before a jump; this is the player's warning.
	[Export]
	public double WindupSeconds { get; set; } = 0.45;

	[Export]
	public float JumpHeight { get; set; } = 190f;

	// Farthest the boss can travel in one jump (it aims at the player).
	[Export]
	public float MaxJumpDistance { get; set; } = 300f;

	// The pause at the top of the jump.
	[Export]
	public double HangSeconds { get; set; } = 0.35;

	[Export]
	public float SlamSpeed { get; set; } = 950f;

	// Landing damage for a player standing on the ground nearby.
	// Being in the air when it lands dodges it.
	[Export]
	public int ImpactDamage { get; set; } = 25;

	[Export]
	public float ImpactRadius { get; set; } = 220f;

	// How long the boss rests after landing (the player's chance to hit).
	[Export]
	public double RecoverSeconds { get; set; } = 1.5;

	// Phase 2 changes.
	[Export]
	public float EnragedSpeedMultiplier { get; set; } = 1.6f;

	[Export]
	public float EnragedJumpMultiplier { get; set; } = 1.5f;

	[Export]
	public double EnragedWindupSeconds { get; set; } = 0.3;

	[Export]
	public double EnragedRecoverSeconds { get; set; } = 0.6;

	// Length of the roar when phase 2 starts.
	[Export]
	public double EnrageSeconds { get; set; } = 1.2;

	public override bool ControlsMovement => true;

	// True once the fight has started (the health bar shows from then on).
	public bool Awake { get; private set; }

	public bool Enraged { get; private set; }

	private enum State { Asleep, Approach, Windup, Rise, Hang, Slam, Recover, Enrage }

	private State _state = State.Asleep;
	private double _timer;
	private double _stateTime;
	private bool _enragePending;

	private HealthComponent _health;
	private AnimatedSprite2D _shockwave;
	private CpuParticles2D _splatter;

	public override void UpdateAI(
		EnemyController enemy, EnemyMovement movement, double delta)
	{
		if (_health == null)
		{
			_health = enemy.GetNode<HealthComponent>("HealthComponent");
			_shockwave = enemy.GetNodeOrNull<AnimatedSprite2D>("Shockwave");
			_splatter = enemy.GetNodeOrNull<CpuParticles2D>("Splatter");
			if (_shockwave != null)
			{
				_shockwave.AnimationFinished += () => _shockwave.Visible = false;
			}
		}

		_timer -= delta;
		_stateTime += delta;

		Node2D player = GetPlayer();
		Vector2 velocity = enemy.Velocity;
		float gravity = movement.Gravity * (float)delta;

		switch (_state)
		{
			case State.Asleep:
				velocity.X = 0;
				enemy.AnimationOverride = "idle";
				if (player != null
					&& enemy.GlobalPosition.DistanceTo(player.GlobalPosition) <= WakeRange)
				{
					Awake = true;
					Enter(State.Approach, ApproachSeconds);
				}
				break;

			case State.Approach:
				if (TryStartEnrage(enemy, ref velocity))
					break;

				float dx = player != null ? player.GlobalPosition.X - enemy.GlobalPosition.X : 0;
				movement.Direction = dx < 0 ? -1 : 1;
				float speed = enemy.MovementSpeed * (Enraged ? EnragedSpeedMultiplier : 1f);
				velocity.X = movement.IsBlockedAhead(enemy) ? 0 : movement.Direction * speed;
				enemy.AnimationOverride = Anim("walk");

				if (_timer <= 0 || Math.Abs(dx) <= JumpTriggerRange)
				{
					velocity.X = 0;
					Enter(State.Windup, Enraged ? EnragedWindupSeconds : WindupSeconds);
				}
				break;

			case State.Windup:
				velocity.X = 0;
				enemy.AnimationOverride = Anim("windup");
				if (_timer <= 0)
				{
					velocity = StartJump(enemy, movement, player);
				}
				break;

			case State.Rise:
				enemy.AnimationOverride = Anim("jump");
				velocity.Y += gravity;
				if (velocity.Y >= 0)
				{
					// Top of the jump: stop and hang above the landing spot.
					velocity = Vector2.Zero;
					Enter(State.Hang, HangSeconds);
				}
				break;

			case State.Hang:
				velocity = Vector2.Zero;
				enemy.AnimationOverride = Anim("hang");
				if (_timer <= 0)
				{
					Enter(State.Slam, 0);
				}
				break;

			case State.Slam:
				velocity = new Vector2(0, SlamSpeed);
				enemy.AnimationOverride = Anim("fall");
				break;

			case State.Recover:
				if (TryStartEnrage(enemy, ref velocity))
					break;

				velocity.X = 0;
				// The splat plays first, then the boss sits dazed.
				enemy.AnimationOverride = Anim(_stateTime < 0.3 ? "land" : "recover");
				if (_timer <= 0)
				{
					Enter(State.Approach, ApproachSeconds);
				}
				break;

			case State.Enrage:
				velocity.X = 0;
				enemy.AnimationOverride = "enrage";
				// Blink white while roaring.
				enemy.SetFlash((int)(_stateTime / 0.1) % 2 == 0 ? 0.5f : 0f);
				if (_timer <= 0)
				{
					enemy.SetFlash(0f);
					Enter(State.Approach, ApproachSeconds);
				}
				break;
		}

		// Gravity everywhere except the jump arc, the hang and the slam.
		if (_state != State.Rise && _state != State.Hang && _state != State.Slam
			&& !enemy.IsOnFloor())
		{
			velocity.Y += gravity;
		}

		enemy.Velocity = velocity;
		enemy.MoveAndSlide();

		if (_state == State.Slam && enemy.IsOnFloor())
		{
			Land(enemy, player);
		}
	}

	public override void OnHurt(EnemyController enemy, EnemyMovement movement)
	{
		// Getting hit wakes the boss even if the player snuck up on it.
		if (!Awake)
		{
			Awake = true;
			Enter(State.Approach, ApproachSeconds);
		}

		// Phase 2 starts the next time the boss is on the ground.
		_health ??= enemy.GetNode<HealthComponent>("HealthComponent");
		if (!Enraged && !_health.IsDead
			&& _health.CurrentHealth <= _health.MaxHealth / 2)
		{
			_enragePending = true;
		}
	}

	private bool TryStartEnrage(EnemyController enemy, ref Vector2 velocity)
	{
		if (!_enragePending || !enemy.IsOnFloor())
			return false;

		_enragePending = false;
		Enraged = true;
		velocity.X = 0;
		enemy.AnimationOverride = "enrage";
		Enter(State.Enrage, EnrageSeconds);
		return true;
	}

	// Launch toward the player so the top of the jump is right above
	// where the boss will land.
	private Vector2 StartJump(EnemyController enemy, EnemyMovement movement, Node2D player)
	{
		float maxDistance = MaxJumpDistance * (Enraged ? EnragedJumpMultiplier : 1f);
		float dx = player != null ? player.GlobalPosition.X - enemy.GlobalPosition.X : 0;
		dx = Math.Clamp(dx, -maxDistance, maxDistance);
		movement.Direction = dx < 0 ? -1 : 1;

		float upSpeed = (float)Math.Sqrt(2 * movement.Gravity * JumpHeight);
		float secondsToTop = upSpeed / movement.Gravity;

		Enter(State.Rise, 0);
		return new Vector2(dx / secondsToTop, -upSpeed);
	}

	private void Land(EnemyController enemy, Node2D player)
	{
		if (_shockwave != null)
		{
			_shockwave.Visible = true;
			_shockwave.Frame = 0;
			_shockwave.Play("burst");
		}

		if (_splatter != null)
		{
			_splatter.Color = Enraged ? new Color(0.87f, 0.27f, 0.35f) : new Color(0.49f, 0.36f, 0.88f);
			_splatter.Restart();
		}

		// The shockwave only hits a player standing on the ground nearby.
		if (player is CharacterBody2D body && body.IsOnFloor() && player is Entity target)
		{
			Vector2 offset = player.GlobalPosition - enemy.GlobalPosition;
			if (Math.Abs(offset.X) <= ImpactRadius && Math.Abs(offset.Y) <= 60f)
			{
				target.TakeDamage(ImpactDamage);
			}
		}

		Enter(State.Recover, Enraged ? EnragedRecoverSeconds : RecoverSeconds);
	}

	// Phase 2 uses the red "_rage" versions of the animations.
	private string Anim(string name)
	{
		return Enraged ? name + "_rage" : name;
	}

	private void Enter(State state, double seconds)
	{
		_state = state;
		_timer = seconds;
		_stateTime = 0;
	}
}
