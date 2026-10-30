using Godot;
using System;

// Handles health-related behavior specific to the player.
public partial class PlayerHealth : Node
{
    // Reference to the reusable HealthComponent.
    private HealthComponent _health = null!;

    public override void _Ready()
    {
        // Get the HealthComponent from the Player node.
        _health = GetParent().GetNode<HealthComponent>(
            "HealthComponent");

        // Subscribe to health-related signals.
        _health.HealthChanged += OnHealthChanged;
        _health.Died += OnPlayerDied;
    }

    // Allow other player systems to read the health values.
    public int CurrentHealth => _health.CurrentHealth;
    public int MaxHealth => _health.MaxHealth;
    public bool IsDead => _health.IsDead;

    // Tell the HealthComponent to apply damage.
    public void TakeDamage(int amount)
    {
        _health.TakeDamage(amount);
    }

    // Tell the HealthComponent to restore health.
    public void Heal(int amount)
    {
        _health.Heal(amount);
    }

    // Restore the player's health to maximum.
    public void RestoreFullHealth()
    {
        _health.RestoreFullHealth();
    }

    // Respond when the player's health changes.
    private void OnHealthChanged(int current, int max)
    {
        GD.Print($"Player Health: {current}/{max}");

        // Later: Update the player's health bar UI.
    }

    // Respond when the player runs out of health.
    private void OnPlayerDied()
    {
        GD.Print("Player has died!");

        // Later: Trigger player death animation.
        // Later: Notify the checkpoint/respawn system.
    }

    // Disconnect signals when this node leaves the scene.
    public override void _ExitTree()
    {
        if (_health != null)
        {
            _health.HealthChanged -= OnHealthChanged;
            _health.Died -= OnPlayerDied;
        }
    }
}