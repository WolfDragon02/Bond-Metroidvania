
using Godot;
using System;

public partial class HealthComponent : Node
{
    // Maximum amount of health a character can have.
    // [Export] lets us change this value in Godot's Inspector.
    [Export]
    public int MaxHealth { get; set; } = 100;

    // The character's current health.
    // Other classes can read it but cannot modify it directly.
    public int CurrentHealth { get; private set; }

    // Returns true when the character has no health left.
    public bool IsDead => CurrentHealth <= 0;

    // Signal triggered whenever health changes.
    [Signal]
    public delegate void HealthChangedEventHandler(
        int currentHealth, int maxHealth);

    // Signal triggered when health reaches zero.
    [Signal]
    public delegate void DiedEventHandler();

    // Runs when the component enters the scene.
    public override void _Ready()
    {
        MaxHealth = Math.Max(1, MaxHealth);

        // Start with full health.
        CurrentHealth = MaxHealth;
    }

    // Reduce health when the character is damaged.
    public void TakeDamage(int amount)
    {
        // Ignore invalid damage or an already dead character.
        if (amount <= 0 || IsDead)
            return;

        // Subtract damage without dropping below zero.
        CurrentHealth = Math.Max(0, CurrentHealth - amount);

        // Notify other systems that health has changed.
        EmitSignal(SignalName.HealthChanged,
            CurrentHealth, MaxHealth);

        // Check whether the character died.
        if (IsDead)
        {
            EmitSignal(SignalName.Died);
        }
    }

    // Restore health.
    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        // Prevent health from exceeding its maximum.
        CurrentHealth = Math.Min(
            MaxHealth, CurrentHealth + amount);

        EmitSignal(SignalName.HealthChanged,
            CurrentHealth, MaxHealth);
    }

    // Restore full health when respawning or resting.
    public void RestoreFullHealth()
    {
        CurrentHealth = MaxHealth;

        EmitSignal(SignalName.HealthChanged,
            CurrentHealth, MaxHealth);
    }
}

