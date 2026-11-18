using Godot;
using System;

// The boss's name and health bar at the bottom of the screen. Lives
// inside the boss scene; shows once the boss wakes up and hides when it
// dies. A light "trail" bar shrinks after each hit to show the damage.
public partial class BossHealthBar : CanvasLayer
{
	[Export]
	public string BossName { get; set; } = "Giant Slime King";

	// Bar colors for phase 1 and the enraged phase 2.
	[Export]
	public Color PhaseOneColor { get; set; } = new Color(0.55f, 0.4f, 0.95f);

	[Export]
	public Color PhaseTwoColor { get; set; } = new Color(0.9f, 0.27f, 0.33f);

	// How fast the trail bar catches up, in health per second.
	[Export]
	public float TrailSpeed { get; set; } = 60f;

	private HealthComponent _health = null!;
	private SlimeKingAI _ai;
	private Control _root = null!;
	private ProgressBar _bar = null!;
	private ProgressBar _trail = null!;
	private StyleBoxFlat _fill = null!;

	public override void _Ready()
	{
		_health = GetParent().GetNode<HealthComponent>("HealthComponent");
		_ai = GetParent().GetNodeOrNull<SlimeKingAI>("SlimeKingAI");
		_root = GetNode<Control>("Root");
		_bar = GetNode<ProgressBar>("Root/Bar");
		_trail = GetNode<ProgressBar>("Root/Trail");
		GetNode<Label>("Root/Name").Text = BossName;

		// Own copy of the fill style so the color can change per phase.
		_fill = (StyleBoxFlat)_bar.GetThemeStylebox("fill").Duplicate();
		_bar.AddThemeStyleboxOverride("fill", _fill);

		_root.Visible = false;
	}

	public override void _Process(double delta)
	{
		bool fighting = !_health.IsDead && (_ai == null || _ai.Awake);
		_root.Visible = fighting;
		if (!fighting)
			return;

		_bar.MaxValue = _health.MaxHealth;
		_trail.MaxValue = _health.MaxHealth;
		_bar.Value = _health.CurrentHealth;
		_trail.Value = Math.Max(_bar.Value,
			Mathf.MoveToward((float)_trail.Value, (float)_bar.Value, TrailSpeed * (float)delta));

		_fill.BgColor = _ai != null && _ai.Enraged ? PhaseTwoColor : PhaseOneColor;
	}
}
