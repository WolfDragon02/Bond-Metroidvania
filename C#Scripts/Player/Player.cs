
using Godot;
using System;

public partial class Player : Entity
{
    public override void _Ready()
    {
        // Initialize the Entity parent class first.
        base._Ready();

        if (Health == null)
        {
            GD.PushError("Player is missing HealthComponent.");
            return;
        }

        // Listen for health changes and death.
        Health.HealthChanged += OnHealthChanged;
        Health.Died += OnPlayerDied;
    }

    // Called whenever the player's health changes.
    private void OnHealthChanged(int current, int max)
    {
        GD.Print($"Player Health: {current}/{max}");
    }

    // Called when the player's health reaches zero.
    private void OnPlayerDied()
    {
        GD.Print("Player has died!");

        // Later: Connect to the checkpoint system.
    }

    // Temporary input for testing the health system.
    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("test_damage"))
        {
            TakeDamage(10);
        }
    }
}
