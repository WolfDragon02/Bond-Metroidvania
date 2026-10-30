using Godot;
using System;

public partial class PlayerMovement : Node
{
    // Controls how quickly the player falls.
    [Export]
    public float Gravity = 980f;

    // Controls how high the player jumps.
    // Negative Y means moving upward in Godot.
    [Export]
    public float JumpVelocity = -350f;

    // Called by PlayerController every physics frame.
    public void UpdateMovement(Entity player, double delta)
    {
        // Get the player's current velocity.
        Vector2 velocity = player.Velocity;

        // Apply gravity when the player is in the air.
        if (!player.IsOnFloor())
        {
            velocity.Y += Gravity * (float)delta;
        }

        // Jump only when standing on the ground.
        if (Input.IsActionJustPressed("jump") &&
            player.IsOnFloor())
        {
            velocity.Y = JumpVelocity;
        }

        // Read left and right movement input.
        float direction = Input.GetAxis(
            "move_left", "move_right");

        // Use MovementSpeed from the Entity class.
        velocity.X = direction * player.MovementSpeed;

        // Update the player's velocity.
        player.Velocity = velocity;

        // Move the player and handle collisions.
        player.MoveAndSlide();
    }
}
