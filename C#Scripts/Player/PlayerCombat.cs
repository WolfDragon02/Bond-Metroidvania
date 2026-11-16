
using Godot;
using System.Collections.Generic;

public partial class PlayerCombat : Node
{
    // Damage for each attack.
    [Export]
    public int LightAttackDamage = 10;

    [Export]
    public int HeavyAttackDamage = 20;

    // References to the player's nodes.
    private AnimatedSprite2D _sprite = null!;
    private Area2D _attackHitbox = null!;

    // Tracks whether the player is attacking.
    private bool _isAttacking = false;

    // Damage for the current attack.
    private int _currentDamage;

    // Prevent hitting the same enemy twice per attack.
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
        // Do not start another attack while attacking.
        if (_isAttacking)
            return;

        // Heavy attack input.
        if (Input.IsActionJustPressed("heavy_attack"))
        {
            Attack("Heavy_Attack", HeavyAttackDamage);
        }
        // Light attack input.
        else if (Input.IsActionJustPressed("light_attack"))
        {
            Attack("Light_Attack", LightAttackDamage);
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

            enemyHealth.TakeDamage(_currentDamage);

            GD.Print($"Enemy took {_currentDamage} damage!");
        }
    }

    private void Attack(string animationName, int damage)
    {
        // Mark the player as attacking.
        _isAttacking = true;

        // Set damage based on the attack type.
        _currentDamage = damage;

        // Start a new attack.
        _hitEnemies.Clear();

        // Play the selected attack animation.
        _sprite.Play(animationName);

        GD.Print($"{animationName}! Damage: {damage}");
    }

    private void OnAnimationFinished()
    {
        string animation = _sprite.Animation.ToString();

        // End either attack when its animation finishes.
        if (animation == "Light_Attack" ||
            animation == "Heavy_Attack")
        {
            _isAttacking = false;
        }
    }

    public override void _ExitTree()
    {
        _sprite.AnimationFinished -= OnAnimationFinished;
    }
}
