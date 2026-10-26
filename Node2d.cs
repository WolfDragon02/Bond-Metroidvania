using Godot;

// Player controller for our side-scrolling 2D Metroidvania.
// The file and C# class names must match for Godot C#.
public partial class Node2d : CharacterBody2D
{
    // Exported values can be adjusted in the Inspector without editing the script.
    [Export] public float MoveSpeed = 230.0f;
    [Export] public float Acceleration = 1800.0f;
    [Export] public float Friction = 1700.0f;
    [Export] public float JumpVelocity = -470.0f;

    // AnimatedSprite2D is a child of the Player node in node_2d.tscn.
    private AnimatedSprite2D _sprite;
    private Vector2 _spawnPosition;

    // Track previous contact states so the debugger does not print every frame.
    private bool _wasOnFloor;
    private bool _wasOnWall;

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _spawnPosition = GlobalPosition;
        _sprite.Play("idle");
    }

    // Physics updates are the correct place for CharacterBody2D movement.
    public override void _PhysicsProcess(double delta)
    {
        float seconds = (float)delta;
        Vector2 velocity = Velocity;

        // 1. Apply gravity whenever the player is not standing on a surface.
        if (!IsOnFloor())
            velocity += GetGravity() * seconds;

        // 2. Jump only when the player is on the floor.
        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // 3. Read left/right actions (-1 for left, +1 for right).
        float horizontalInput = Input.GetAxis("move_left", "move_right");

        // 4. Accelerate toward the desired speed, or slow down when released.
        if (!Mathf.IsZeroApprox(horizontalInput))
        {
            velocity.X = Mathf.MoveToward(
                velocity.X, horizontalInput * MoveSpeed, Acceleration * seconds);

            // Face the direction of movement without changing the collision shape.
            _sprite.FlipH = horizontalInput < 0.0f;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0.0f, Friction * seconds);
        }

        // 5. Commit movement and slide along physical walls/floors/platforms.
        Velocity = velocity;
        MoveAndSlide();

        // 6. Choose the appropriate animation AFTER movement updates floor state.
        UpdateAnimation();

        // 7. Show first-contact messages for easier collision debugging.
        ReportSolidCollisions();
    }

    private void UpdateAnimation()
    {
        string animation;

        // Airborne states take priority over standing/running animations.
        if (!IsOnFloor())
            animation = Velocity.Y < 0.0f ? "jump" : "fall";
        else if (Mathf.Abs(Velocity.X) > 5.0f)
            animation = "walk";
        else
            animation = "idle";

        // Only change animation when the state changes; otherwise it restarts.
        if (_sprite.Animation != animation || !_sprite.IsPlaying())
            _sprite.Play(animation);
    }

    private void ReportSolidCollisions()
    {
        if (IsOnFloor() && !_wasOnFloor)
            GD.Print("Player landed on a solid surface.");

        if (IsOnWall() && !_wasOnWall)
        {
            // MoveAndSlide may produce several contacts; inspect all of them.
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                KinematicCollision2D contact = GetSlideCollision(i);
                if (contact.GetCollider() is Node collider)
                {
                    GD.Print("Player collided with: ", collider.Name);
                    break;
                }
            }
        }

        _wasOnFloor = IsOnFloor();
        _wasOnWall = IsOnWall();
    }

    // Called by a hazard (Area2D) when the player touches it.
    public void Respawn()
    {
        GD.Print("Hazard detected: returning player to the starting point.");
        GlobalPosition = _spawnPosition;
        Velocity = Vector2.Zero;
        _wasOnFloor = false;
        _wasOnWall = false;
    }
}
