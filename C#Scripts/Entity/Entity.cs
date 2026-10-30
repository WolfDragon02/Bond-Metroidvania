using Godot;
using System;

// Base class for Player, Familiar, and Enemy.
public abstract partial class Entity : CharacterBody2D
{
    // Shared movement attribute.
    [Export]
    public float MovementSpeed { get; set; } = 100f;

    // Reference to the HealthComponent child node.
    protected HealthComponent? Health { get; private set; }

    public override void _Ready()
    {
        // Retrieve the HealthComponent from the scene tree.
        // A Familiar without health can leave this null.
        Health = GetNodeOrNull<HealthComponent>("HealthComponent");
    }

    // Shared method for taking damage.
    public void TakeDamage(int amount)
    {
        Health?.TakeDamage(amount);
    }

    // Shared method for healing.
    public void Heal(int amount)
    {
        Health?.Heal(amount);
    }
}

