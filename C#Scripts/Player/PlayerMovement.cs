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

	// Controls how fast the player moves during a dash.
	[Export]
	public float DashSpeed = 650f;

	// Controls how long the dash lasts.
	[Export]
	public float DashDuration = 0.15f;

	// Controls how long the player must wait before dashing again.
	[Export]
	public float DashCooldown = 0.5f;

	// Tracks whether the player is currently dashing.
	private bool _isDashing = false;

	// Tracks the remaining dash time.
	private float _dashTimeRemaining = 0f;

	// Tracks the remaining cooldown time.
	private float _dashCooldownRemaining = 0f;

	// Remembers the last direction the player moved.
	// 1 means right and -1 means left.
	private float _lastDirection = 1f;

	// Allows other scripts, such as PlayerAnimation,
	// to check whether the player is dashing.
	public bool IsDashing => _isDashing;

	// Called by PlayerController every physics frame.
	public void UpdateMovement(Entity player, double delta)
	{
		float deltaTime = (float)delta;

		// Reduce the dash cooldown.
		if (_dashCooldownRemaining > 0f)
		{
			_dashCooldownRemaining -= deltaTime;
		}

		// Read left and right movement input.
		float direction = Input.GetAxis(
			"move_left", "move_right");

		// Remember the last direction the player moved.
		if (direction > 0f)
		{
			_lastDirection = 1f;
		}
		else if (direction < 0f)
		{
			_lastDirection = -1f;
		}

		// Start the dash when the dash input is pressed.
		if (Input.IsActionJustPressed("dash") &&
			!_isDashing &&
			_dashCooldownRemaining <= 0f)
		{
			StartDash();
		}

		// Get the player's current velocity.
		Vector2 velocity = player.Velocity;

		// Handle dash movement separately from normal movement.
		if (_isDashing)
		{
			velocity.X = _lastDirection * DashSpeed;
			velocity.Y = 0f;

			player.Velocity = velocity;
			player.MoveAndSlide();

			_dashTimeRemaining -= deltaTime;

			// End the dash when its timer reaches zero.
			if (_dashTimeRemaining <= 0f)
			{
				_isDashing = false;
				_dashCooldownRemaining = DashCooldown;

				player.Velocity = new Vector2(
					0f,
					player.Velocity.Y
				);
			}

			// Skip normal movement while dashing.
			return;
		}

		// Apply gravity when the player is in the air.
		if (!player.IsOnFloor())
		{
			velocity.Y += Gravity * deltaTime;
		}

		// Jump only when standing on the ground.
		if (Input.IsActionJustPressed("jump") &&
			player.IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		// Use MovementSpeed from the Entity class.
		velocity.X = direction * player.MovementSpeed;

		// Update the player's velocity.
		player.Velocity = velocity;

		// Move the player and handle collisions.
		player.MoveAndSlide();
	}

	private void StartDash()
	{
		_isDashing = true;
		_dashTimeRemaining = DashDuration;
	}
}
