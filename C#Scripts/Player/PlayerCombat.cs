
using Godot;
using System;
public partial class PlayerCombat : Node
{
    // Reference to the player's sprite.
    private AnimatedSprite2D _sprite = null!;

    // Check if the player is currently attacking.
    private bool _isAttacking = false;

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<AnimatedSprite2D>(
            "AnimatedSprite2D");

        // Detect when an animation finishes.
        _sprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _Process(double delta)
    {
        // Attack when J is pressed.
        if (Input.IsActionJustPressed("light_attack")
            && !_isAttacking)
        {
            Light_Attack();
        }
    }

    private void Light_Attack()
    {
        _isAttacking = true;

        // Play the attack animation.
        _sprite.Play("Light_Attack");

        GD.Print("Player performed a light attack!");
    }

    private void OnAnimationFinished()
    {
        if (_sprite.Animation.ToString() == "Light_Attack")
        {
            _isAttacking = false;
        }
    }

    public override void _ExitTree()
    {
        _sprite.AnimationFinished -= OnAnimationFinished;
    }
}

