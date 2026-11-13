using Godot;
using System;
using System.Linq;

// Coordinates the enemy's separate modules.
public partial class EnemyController : Entity
{
	// Damage dealt to the player when they touch the enemy. 0 turns it off.
	[Export]
	public int ContactDamage { get; set; } = 1;

	// Seconds between contact hits, so standing inside an enemy
	// doesn't drain health every frame.
	[Export]
	public double ContactCooldown { get; set; } = 1.0;

	// How long the enemy freezes and flashes white after a hit.
	[Export]
	public double HurtSeconds { get; set; } = 0.2;

	// Animation an EnemyAI wants instead of walk/idle, e.g. "charge".
	// Null lets the controller pick walk or idle from the movement.
	public string AnimationOverride { get; set; }

	public bool IsDead { get; private set; }

	private EnemyMovement _movement = null!;
	private HealthComponent _health = null!;

	// Optional behavior, e.g. ChargerAI. The Crawler has none.
	private EnemyAI _ai;

	// Optional nodes; an enemy without them still works.
	private AnimatedSprite2D _sprite;
	private ShaderMaterial _flashMaterial;
	private Area2D _contactHitbox;

	private int _lastHealth;
	private double _hurtTimer;
	private double _contactTimer;

	public override void _Ready()
	{
		// Initialize shared Entity functionality.
		base._Ready();

		_movement = GetNode<EnemyMovement>("EnemyMovement");
		_ai = GetChildren().OfType<EnemyAI>().FirstOrDefault();
		_sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
		_flashMaterial = _sprite?.Material as ShaderMaterial;
		_contactHitbox = GetNodeOrNull<Area2D>("ContactHitbox");

		_health = GetNode<HealthComponent>("HealthComponent");
		_lastHealth = _health.CurrentHealth;
		_health.HealthChanged += OnHealthChanged;
		_health.Died += OnDied;

		if (_sprite != null)
		{
			_sprite.AnimationFinished += OnAnimationFinished;
		}

		// Walk through the player instead of treating them as a wall.
		// The enemy hurts the player with its ContactHitbox instead.
		if (GetTree().GetFirstNodeInGroup("player") is PhysicsBody2D player)
		{
			AddCollisionExceptionWith(player);
		}
	}

	// Let the AI steer, then update movement every physics frame.
	public override void _PhysicsProcess(double delta)
	{
		// The death animation plays in place, then the enemy is removed.
		if (IsDead)
			return;

		bool hurt = _hurtTimer > 0;
		if (hurt)
		{
			// Freeze and flash for a moment after taking damage.
			_hurtTimer -= delta;
			SetFlash(_hurtTimer > 0 ? 1f : 0f);
		}
		else
		{
			_ai?.UpdateAI(this, _movement, delta);
		}

		_movement.UpdateMovement(this, delta, canWalk: !hurt);

		_contactTimer -= delta;
		DealContactDamage();
		UpdateSprite(hurt);
	}

	// Tints the sprite toward white: 0 is normal, 1 is fully white.
	public void SetFlash(float amount)
	{
		_flashMaterial?.SetShaderParameter("flash", amount);
	}

	private void DealContactDamage()
	{
		if (_contactHitbox == null || ContactDamage <= 0 || _contactTimer > 0)
			return;

		foreach (Node2D body in _contactHitbox.GetOverlappingBodies())
		{
			if (body.IsInGroup("player") && body is Entity player)
			{
				player.TakeDamage(ContactDamage);
				_contactTimer = ContactCooldown;
				return;
			}
		}
	}

	// Face the walking direction and pick the animation to play.
	private void UpdateSprite(bool hurt)
	{
		if (_sprite == null)
			return;

		// The art faces right.
		_sprite.FlipH = _movement.Direction < 0;
		_sprite.SpeedScale = hurt ? 0f : 1f;

		bool moving = Math.Abs(Velocity.X) > 1f;
		string animation = AnimationOverride
			?? (moving || !_sprite.SpriteFrames.HasAnimation("idle") ? "walk" : "idle");

		if (_sprite.Animation.ToString() != animation
			&& _sprite.SpriteFrames.HasAnimation(animation))
		{
			_sprite.Play(animation);
		}
	}

	private void OnHealthChanged(int currentHealth, int maxHealth)
	{
		if (currentHealth < _lastHealth && !IsDead)
		{
			_hurtTimer = HurtSeconds;
			_ai?.OnHurt(this, _movement);
		}

		_lastHealth = currentHealth;
	}

	private void OnDied()
	{
		IsDead = true;
		SetFlash(0f);

		// Stop blocking or hurting anything while the death animation plays.
		GetNode<CollisionShape2D>("CollisionShape2D")
			.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_contactHitbox?.SetDeferred(Area2D.PropertyName.Monitoring, false);

		if (_sprite != null && _sprite.SpriteFrames.HasAnimation("death"))
		{
			_sprite.SpeedScale = 1f;
			_sprite.Play("death");
		}
		else
		{
			QueueFree();
		}
	}

	private void OnAnimationFinished()
	{
		if (IsDead)
		{
			QueueFree();
		}
	}
}
