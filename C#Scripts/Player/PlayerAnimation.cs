using Godot;
using System;

public partial class PlayerAnimation : Node
{
    // Reference to the player's AnimatedSprite2D.
    private AnimatedSprite2D _sprite = null!;

    public override void _Ready()
    {
        // Find AnimatedSprite2D on the parent Player node.
        _sprite = GetParent().GetNode<AnimatedSprite2D>(
            "AnimatedSprite2D");
    }

    // Update the animation based on the player's movement.
    public void UpdateAnimation(CharacterBody2D player)
    {
        // Get the player's horizontal velocity.
        float direction = player.Velocity.X;

        // Check if the player is moving left or right.
        if (Mathf.Abs(direction) > 0.1f)
        {
            // Flip the sprite when moving left.
            // Assumes the original sprite faces right.
            _sprite.FlipH = direction < 0;

            // Play walking animation.
            PlayAnimation("walk");
        }
        else
        {
            // Play idle animation when standing still.
            PlayAnimation("idle");
        }
    }

    // Avoid restarting the same animation every frame.
    private void PlayAnimation(string animationName)
    {
        if (_sprite.Animation.ToString() != animationName
            || !_sprite.IsPlaying())
        {
            _sprite.Play(animationName);
        }
    }
}
