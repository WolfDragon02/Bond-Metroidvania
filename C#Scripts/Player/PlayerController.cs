
using Godot;
using System;

public partial class PlayerController : Entity
{
    // References to the player's separate modules.
    private PlayerMovement _movement = null!;
    private PlayerHealth _playerHealth = null!;

    // NEW: Reference to the player's animation module.
    private PlayerAnimation _playerAnimation = null!;

    public override void _Ready()
    {
        // Initialize shared Entity functionality.
        base._Ready();

        // Connect the player modules.
        _movement = GetNode<PlayerMovement>("PlayerMovement");
        _playerHealth = GetNode<PlayerHealth>("PlayerHealth");

        // NEW: Find the PlayerAnimation node.
        _playerAnimation = GetNode<PlayerAnimation>("PlayerAnimation");
    }

    // Update movement and animation every physics frame.
    public override void _PhysicsProcess(double delta)
    {
        // First, update player movement and velocity.
        _movement.UpdateMovement(this, delta);

        // NEW: Update animation based on the player's velocity.
        _playerAnimation.UpdateAnimation(this);
    }

    // Temporary input for testing the health system.
    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("test_damage"))
        {
            _playerHealth.TakeDamage(10);
        }
    }
}
