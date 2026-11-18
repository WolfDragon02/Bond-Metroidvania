using Godot;
using System;

// A small health bar that floats above an enemy while it is "in focus":
// the player is close by, or the enemy was hit in the last few seconds.
// It fades in and out, the fill shifts from green to red as health drops,
// and a white trail drains behind the fill so every hit is easy to read.
public partial class EnemyHealthBar : Node2D
{
	// Size of the fill area in pixels (the border is drawn around it).
	[Export]
	public float Width { get; set; } = 56f;

	[Export]
	public float Height { get; set; } = 6f;

	// The bar shows while the player is within this distance.
	[Export]
	public float FocusRange { get; set; } = 300f;

	// The bar stays up this long after a hit, even if the player backs off.
	[Export]
	public double LingerSeconds { get; set; } = 2.5;

	// How fast the bar fades in and out (opacity per second).
	[Export]
	public float FadeSpeed { get; set; } = 6f;

	// The white trail waits this long, then drains down to the new health.
	[Export]
	public double TrailDelay { get; set; } = 0.35;

	// Trail drain speed, in fractions of the full bar per second.
	[Export]
	public float TrailSpeed { get; set; } = 1.2f;

	private static readonly Color Outline = new(0.08f, 0.06f, 0.1f);
	private static readonly Color Back = new(0.22f, 0.18f, 0.26f);
	private static readonly Color Trail = new(1f, 0.95f, 0.85f);
	private static readonly Color High = new(0.4f, 0.85f, 0.35f);
	private static readonly Color Mid = new(0.98f, 0.78f, 0.22f);
	private static readonly Color Low = new(0.92f, 0.25f, 0.25f);

	private Node2D _enemy;
	private HealthComponent _health;
	private Node2D _player;

	private float _shown = 1f;       // current health, 0..1
	private float _trail = 1f;       // where the white trail ends, 0..1
	private double _trailWait;
	private double _sinceHit = double.MaxValue;
	private float _hitFlash;         // 1 right after a hit, fades to 0
	private float _alpha;
	private bool _dead;
	private double _sinceDeath;

	public override void _Ready()
	{
		_enemy = GetParent<Node2D>();
		_health = _enemy.GetNode<HealthComponent>("HealthComponent");
		_health.HealthChanged += OnHealthChanged;
		_health.Died += () => _dead = true;

		// Draw above the level art and the enemy itself.
		ZIndex = 10;
		Modulate = new Color(1, 1, 1, 0);
		Visible = false;
	}

	private void OnHealthChanged(int current, int max)
	{
		float fraction = Math.Clamp((float)current / Math.Max(1, max), 0f, 1f);
		if (fraction < _shown)
		{
			_sinceHit = 0;
			_trailWait = TrailDelay;
			_hitFlash = 1f;
		}

		_shown = fraction;
		// Healing jumps the trail up with the fill.
		_trail = Math.Max(_trail, _shown);
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		_sinceHit += delta;
		_hitFlash = Mathf.MoveToward(_hitFlash, 0f, d * 5f);

		_trailWait -= delta;
		if (_trailWait <= 0)
		{
			_trail = Mathf.MoveToward(_trail, _shown, TrailSpeed * d);
		}

		// After death, hold the empty bar for a moment, then fade it out.
		if (_dead)
			_sinceDeath += delta;

		bool focused = _dead
			? _sinceDeath < 0.3
			: _sinceHit < LingerSeconds || PlayerIsNear();

		_alpha = Mathf.MoveToward(_alpha, focused ? 1f : 0f, FadeSpeed * d);
		Modulate = new Color(1, 1, 1, _alpha);
		Visible = _alpha > 0f;

		if (Visible)
			QueueRedraw();
	}

	private bool PlayerIsNear()
	{
		if (_player == null || !IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (_player == null)
				return false;
		}

		return _enemy.GlobalPosition.DistanceTo(_player.GlobalPosition) <= FocusRange;
	}

	public override void _Draw()
	{
		float left = -Width / 2f;
		float top = -Height / 2f;

		// Border, then the empty background.
		DrawRect(new Rect2(left - 2, top - 2, Width + 4, Height + 4), Outline);
		DrawRect(new Rect2(left, top, Width, Height), Back);

		// White trail showing the damage just taken.
		if (_trail > _shown)
		{
			DrawRect(new Rect2(left, top, Width * _trail, Height), Trail);
		}

		// The health fill, flashing white right after a hit.
		float fillWidth = Mathf.Round(Width * _shown);
		if (fillWidth > 0)
		{
			Color fill = FillColor(_shown).Lerp(Colors.White, _hitFlash * 0.7f);
			DrawRect(new Rect2(left, top, fillWidth, Height), fill);

			// A lighter strip along the top gives it a pixel-art shine.
			DrawRect(new Rect2(left, top, fillWidth, 2), fill.Lightened(0.35f));
		}
	}

	// Green when healthy, yellow around half, red when nearly dead.
	private static Color FillColor(float fraction)
	{
		return fraction > 0.5f
			? Mid.Lerp(High, (fraction - 0.5f) * 2f)
			: Low.Lerp(Mid, fraction * 2f);
	}
}
