using Godot;
using System;

// The Giant Slime King boss.
// Phase 1 (above half health): hops toward the player, then picks an
// attack. Up close it jumps, hangs in the air above where it will land,
// and slams down with a damaging shockwave, then rests so the player can
// attack. When the player keeps their distance (and every few attacks) it
// spits a spread of slime globs that land around the player instead.
// Phase 2 (half health or less): enraged. Hops faster and farther, spits
// more globs, slams twice in a row, splashes globs out when it lands and
// rests for less time.
public partial class SlimeKingAI : EnemyAI
{
	// The fight starts when the player comes this close.
	[Export]
	public float WakeRange { get; set; } = 500f;

	// Longest the boss hops toward the player before it attacks anyway.
	[Export]
	public double ApproachSeconds { get; set; } = 2.4;

	// The boss jump-slams as soon as the player is this close.
	[Export]
	public float JumpTriggerRange { get; set; } = 260f;

	// ---- Hopping (how the boss gets around)

	[Export]
	public float HopHeight { get; set; } = 60f;

	// Farthest one hop travels.
	[Export]
	public float HopDistance { get; set; } = 110f;

	// Squish on the ground between hops.
	[Export]
	public double HopPauseSeconds { get; set; } = 0.3;

	// ---- Jump slam

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

	// ---- Spitting slime globs

	[Export]
	public PackedScene SlimeBallScene { get; set; }

	// The boss spits instead of jumping when the player is farther than this.
	[Export]
	public float SpitRange { get; set; } = 380f;

	// Every this many attacks is a spit, even up close. 0 turns it off.
	[Export]
	public int SpitEvery { get; set; } = 3;

	// The breath in before spitting; this is the player's warning.
	[Export]
	public double InhaleSeconds { get; set; } = 0.55;

	[Export]
	public int GlobsPerVolley { get; set; } = 3;

	// Gap between where the globs land, centered on the player.
	[Export]
	public float GlobSpacing { get; set; } = 95f;

	[Export]
	public double GlobFlightSeconds { get; set; } = 0.95;

	[Export]
	public double SpitRecoverSeconds { get; set; } = 0.6;

	// ---- Phase 2 changes

	[Export]
	public float EnragedSpeedMultiplier { get; set; } = 1.6f;

	[Export]
	public float EnragedJumpMultiplier { get; set; } = 1.5f;

	[Export]
	public double EnragedWindupSeconds { get; set; } = 0.3;

	[Export]
	public double EnragedRecoverSeconds { get; set; } = 0.6;

	[Export]
	public double EnragedInhaleSeconds { get; set; } = 0.35;

	[Export]
	public int EnragedGlobsPerVolley { get; set; } = 5;

	// Globs splashed out to each side when the enraged boss lands.
	[Export]
	public int SplashGlobsPerSide { get; set; } = 2;

	// Length of the roar when phase 2 starts.
	[Export]
	public double EnrageSeconds { get; set; } = 1.2;

	// How fast a player stuck under the boss is pushed out to its side.
	[Export]
	public float PushOutSpeed { get; set; } = 900f;

	// Half the width of the boss's contact hitbox plus the player's width,
	// so a pushed-out player is clear of contact damage.
	[Export]
	public float PushOutHalfWidth { get; set; } = 98f;

	public override bool ControlsMovement => true;

	// True once the fight has started (the health bar shows from then on).
	public bool Awake { get; private set; }

	public bool Enraged { get; private set; }

	private enum State { Asleep, Approach, Windup, Rise, Hang, Slam, Recover, Inhale, Spit, Enrage }

	private State _state = State.Asleep;
	private double _timer;
	private double _stateTime;
	private double _hopPause;
	private bool _wasOnFloor = true;
	private bool _enragePending;
	private bool _secondSlamDone;
	private int _attackCount;
	private int _pushDirection;

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
		float dx = player != null ? player.GlobalPosition.X - enemy.GlobalPosition.X : 0;
		bool onFloor = enemy.IsOnFloor();

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

				if (!onFloor)
				{
					// Mid-hop: keep the hop's speed and pick the right pose.
					enemy.AnimationOverride = Anim(velocity.Y < 0 ? "jump" : "fall");
					break;
				}

				velocity.X = 0;
				movement.Direction = dx < 0 ? -1 : 1;
				enemy.AnimationOverride = Anim("land");

				if (!_wasOnFloor)
				{
					// Just landed a hop: squish for a moment.
					_hopPause = Enraged ? HopPauseSeconds / 2 : HopPauseSeconds;
				}

				_hopPause -= delta;
				if (_timer <= 0 || Math.Abs(dx) <= JumpTriggerRange)
				{
					ChooseAttack(dx);
				}
				else if (_hopPause <= 0)
				{
					velocity = Hop(enemy, movement, dx);
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
				// The shockwave is this attack's hit, not touching the boss.
				enemy.DelayContactDamage(0.1);
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

			case State.Inhale:
				velocity.X = 0;
				movement.Direction = dx < 0 ? -1 : 1;
				enemy.AnimationOverride = Anim("inhale");
				if (_timer <= 0)
				{
					SpitVolley(enemy, movement, player);
					Enter(State.Spit, SpitRecoverSeconds);
				}
				break;

			case State.Spit:
				velocity.X = 0;
				enemy.AnimationOverride = Anim("spit");
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
			&& !onFloor)
		{
			velocity.Y += gravity;
		}

		enemy.Velocity = velocity;
		enemy.MoveAndSlide();
		_wasOnFloor = enemy.IsOnFloor();

		if (_state == State.Slam && _wasOnFloor)
		{
			Land(enemy, movement, player);
		}

		PushPlayerOut(enemy, player, delta);
	}

	// The player passes through enemies, so after the boss lands on them
	// they would be stuck inside it taking contact damage. While the boss
	// is on the ground, slide a grounded player out to the nearer side
	// (or the other side if a wall is in the way). Jumping over the boss
	// is left alone.
	private void PushPlayerOut(EnemyController enemy, Node2D player, double delta)
	{
		if (player is not CharacterBody2D body || !enemy.IsOnFloor() || !body.IsOnFloor())
		{
			_pushDirection = 0;
			return;
		}

		Vector2 offset = player.GlobalPosition - enemy.GlobalPosition;
		bool inside = Math.Abs(offset.X) < PushOutHalfWidth
			&& offset.Y > -112f && offset.Y < 10f;
		if (!inside)
		{
			_pushDirection = 0;
			return;
		}

		if (_pushDirection == 0)
		{
			_pushDirection = offset.X < 0 ? -1 : 1;
		}

		float step = Math.Min(PushOutSpeed * (float)delta,
			PushOutHalfWidth - Math.Abs(offset.X) * Math.Sign(offset.X) * _pushDirection);
		var hit = body.MoveAndCollide(new Vector2(_pushDirection * Math.Max(step, 1f), 0));
		if (hit != null && Math.Abs(hit.GetNormal().X) > 0.5f)
		{
			// Blocked by a wall: push toward the other side instead.
			_pushDirection = -_pushDirection;
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

	// Spit when the player is far away and every few attacks; otherwise slam.
	private void ChooseAttack(float dx)
	{
		_attackCount++;
		bool spitTurn = SpitEvery > 0 && _attackCount % SpitEvery == 0;
		if (SlimeBallScene != null && (Math.Abs(dx) > SpitRange || spitTurn))
		{
			Enter(State.Inhale, Enraged ? EnragedInhaleSeconds : InhaleSeconds);
		}
		else
		{
			_secondSlamDone = false;
			Enter(State.Windup, Enraged ? EnragedWindupSeconds : WindupSeconds);
		}
	}

	// A small hop toward the player. Hops stop short at walls.
	private Vector2 Hop(EnemyController enemy, EnemyMovement movement, float dx)
	{
		float reach = HopDistance * (Enraged ? EnragedSpeedMultiplier : 1f);
		float distance = movement.IsBlockedAhead(enemy) ? 0 : Math.Min(Math.Abs(dx), reach);

		float upSpeed = (float)Math.Sqrt(2 * movement.Gravity * HopHeight);
		float airSeconds = 2 * upSpeed / movement.Gravity;
		return new Vector2(movement.Direction * distance / airSeconds, -upSpeed);
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

	private void Land(EnemyController enemy, EnemyMovement movement, Node2D player)
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
				// One hit per landing: no contact damage on top of the shockwave.
				enemy.DelayContactDamage(enemy.ContactCooldown);
			}
		}

		if (!Enraged)
		{
			Enter(State.Recover, RecoverSeconds);
			return;
		}

		// Phase 2: globs splash out both ways, and the first slam is
		// followed right away by a second one.
		for (int i = 0; i < SplashGlobsPerSide; i++)
		{
			float speed = 200f + 160f * i;
			float up = 430f - 90f * i;
			ThrowGlob(enemy, enemy.GlobalPosition + new Vector2(-60, -40), new Vector2(-speed, -up));
			ThrowGlob(enemy, enemy.GlobalPosition + new Vector2(60, -40), new Vector2(speed, -up));
		}

		if (!_secondSlamDone)
		{
			_secondSlamDone = true;
			Enter(State.Windup, EnragedWindupSeconds);
		}
		else
		{
			Enter(State.Recover, EnragedRecoverSeconds);
		}
	}

	// Spits a spread of globs timed to land around where the player stands,
	// so the player has to move to a gap between them.
	private void SpitVolley(EnemyController enemy, EnemyMovement movement, Node2D player)
	{
		if (player == null)
			return;

		int count = Enraged ? EnragedGlobsPerVolley : GlobsPerVolley;
		float flight = (float)(Enraged ? GlobFlightSeconds * 0.8 : GlobFlightSeconds);
		float gravity = movement.Gravity;
		Vector2 mouth = enemy.GlobalPosition + new Vector2(movement.Direction * 28, -60);

		for (int i = 0; i < count; i++)
		{
			float offset = (i - (count - 1) / 2f) * GlobSpacing;
			Vector2 target = player.GlobalPosition + new Vector2(offset, 0);
			Vector2 velocity = new(
				(target.X - mouth.X) / flight,
				(target.Y - mouth.Y - 0.5f * gravity * flight * flight) / flight);
			ThrowGlob(enemy, mouth, velocity);
		}
	}

	private void ThrowGlob(EnemyController enemy, Vector2 from, Vector2 velocity)
	{
		if (SlimeBallScene == null)
			return;

		SlimeBall glob = SlimeBallScene.Instantiate<SlimeBall>();
		enemy.GetParent().AddChild(glob);
		glob.GlobalPosition = from;
		glob.Launch(velocity, Enraged);
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
