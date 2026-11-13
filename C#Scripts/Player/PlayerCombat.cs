
using Godot;
using System.Collections.Generic;

public partial class PlayerCombat : Node
{
    // Damage dealt by a basic attack.
    [Export]
    public int AttackDamage = 10;

    // References to the player's nodes.
    private AnimatedSprite2D _sprite = null!;
    private Area2D _attackHitbox = null!;

    // Tracks whether the player is attacking.
    private bool _isAttacking = false;

    // Prevents hitting the same enemy twice per attack.
    private HashSet<Node2D> _hitEnemies = new();

    private float _hitboxDistance;

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<AnimatedSprite2D>(
            "AnimatedSprite2D");

        _attackHitbox = GetParent().GetNode<Area2D>(
            "AttackHitbox");

        _hitboxDistance = Mathf.Abs(
            _attackHitbox.Position.X);

        _sprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("light_attack")
            && !_isAttacking)
        {
            Attack();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Keep the attack hitbox in front of the player.
        Vector2 position = _attackHitbox.Position;

        position.X = _sprite.FlipH
            ? -_hitboxDistance
            : _hitboxDistance;

        _attackHitbox.Position = position;

        if (!_isAttacking)
            return;

        // Check for enemies inside the hitbox.
        foreach (Node2D body in _attackHitbox.GetOverlappingBodies())
        {
            // Do not hit the player.
            if (body == GetParent())
                continue;

            // Find the enemy's health component.
            HealthComponent? enemyHealth =
                body.GetNodeOrNull<HealthComponent>(
                    "HealthComponent");

            if (enemyHealth == null)
                continue;

            // Only damage each enemy once per attack.
            if (!_hitEnemies.Add(body))
                continue;

            enemyHealth.TakeDamage(AttackDamage);

            GD.Print($"Enemy took {AttackDamage} damage!");
        }
    }

    private void Attack()
    {
        _isAttacking = true;

        // Start a new attack.
        _hitEnemies.Clear();

        // Play attack animation.
        _sprite.Play("Light_Attack");

        GD.Print($"Light Attack! Damage: {AttackDamage}");
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
