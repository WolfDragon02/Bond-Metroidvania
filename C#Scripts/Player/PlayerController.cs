
using Godot;
using System;

public partial class PlayerController : Entity
{
    // References to the player's separate modules.
    private PlayerMovement _movement = null!;
    private PlayerHealth _playerHealth = null!;

    public override void _Ready()
    {
        // Initialize shared Entity functionality.
        base._Ready();

        // Connect the player modules.
        _movement = GetNode<PlayerMovement>("PlayerMovement");
        _playerHealth = GetNode<PlayerHealth>("PlayerHealth");
    }

    // Update movement every physics frame.
    public override void _PhysicsProcess(double delta)
    {
        _movement.UpdateMovement(this, delta);
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
