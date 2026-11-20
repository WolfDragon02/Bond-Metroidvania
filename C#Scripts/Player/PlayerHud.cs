
using Godot;
using System;

public partial class PlayerHUD : CanvasLayer
{
    [Export] public PlayerHealth Health { get; set; } = null!;
    [Export] public PlayerSpecialPoints Special { get; set; } = null!;

    private ProgressBar _healthBar = null!;
    private ProgressBar _specialBar = null!;

    private Label _healthLabel = null!;
    private Label _specialLabel = null!;

    public override void _Ready()
    {
        // Find the HUD nodes.
        _healthBar = GetNode<ProgressBar>(
            "MarginContainer/VBoxContainer/HealthBar");

        _specialBar = GetNode<ProgressBar>(
            "MarginContainer/VBoxContainer/SpecialBar");

        _healthLabel = GetNode<Label>(
            "MarginContainer/VBoxContainer/HealthLabel");

        _specialLabel = GetNode<Label>(
            "MarginContainer/VBoxContainer/SpecialLabel");

        if (Health == null || Special == null)
        {
            GD.PushError("Assign Health and Special in PlayerHUD.");
            return;
        }

        // Listen for changes.
        Health.HealthChanged += UpdateHealth;
        Special.PointsChanged += UpdateSpecial;

        // Display starting values.
        UpdateHealth(Health.CurrentHealth, Health.MaxHealth);
        UpdateSpecial(Special.CurrentPoints, Special.MaxPoints);
    }

    private void UpdateHealth(int current, int max)
    {
        _healthBar.MinValue = 0;
        _healthBar.MaxValue = Math.Max(1, max);
        _healthBar.Value = Math.Clamp(current, 0, Math.Max(0, max));

        _healthLabel.Text = $"Health: {current}/{max}";
    }

    private void UpdateSpecial(int current, int max)
    {
        _specialBar.MinValue = 0;
        _specialBar.MaxValue = Math.Max(1, max);
        _specialBar.Value = Math.Clamp(current, 0, Math.Max(0, max));

        _specialLabel.Text = $"Special Points: {current}/{max}";
    }

    public override void _ExitTree()
    {
        if (Health != null)
            Health.HealthChanged -= UpdateHealth;

        if (Special != null)
            Special.PointsChanged -= UpdateSpecial;
    }
}
