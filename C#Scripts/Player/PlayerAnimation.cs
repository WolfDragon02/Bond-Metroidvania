
using Godot;
using System;
public partial class PlayerAnimation : Node
{
    private AnimatedSprite2D _sprite = null!;

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<AnimatedSprite2D>(
            "AnimatedSprite2D");
    }

    public void UpdateAnimation(CharacterBody2D player)
    {
        // Do not interrupt the attack animation.
        if (_sprite.Animation.ToString() == "Light_Attack"
            && _sprite.IsPlaying())
        {
            return;
        }

        float direction = player.Velocity.X;

        if (Mathf.Abs(direction) > 0.1f)
        {
            // Flip sprite based on movement direction.
            _sprite.FlipH = direction < 0;

            PlayAnimation("Walk");
        }
        else
        {
            PlayAnimation("Idle");
        }
    }

    private void PlayAnimation(string animationName)
    {
        if (_sprite.Animation.ToString() != animationName
            || !_sprite.IsPlaying())
        {
            _sprite.Play(animationName);
        }
    }
}
