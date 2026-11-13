using Godot;
using System;

public partial class PlayerController : Entity
{
	// References to the player's separate modules.
	private PlayerMovement _movement = null!;
	private PlayerHealth _playerHealth = null!;
	private PlayerAnimation _playerAnimation = null!;

	// Dash settings. These can be changed in the Inspector.
	[Export] public float DashSpeed = 650.0f;
	[Export] public float DashDuration = 0.15f;
	[Export] public float DashCooldown = 0.5f;

	// Dash state.
	private bool _isDashing = false;
	private float _dashTimeRemaining = 0.0f;
	private float _dashCooldownRemaining = 0.0f;

	// Remembers the last direction the player moved.
	// 1 means right and -1 means left.
	private float _lastDirection = 1.0f;

	public override void _Ready()
	{
		// Initialize shared Entity functionality.
		base._Ready();

		// Connect the player modules.
		_movement = GetNode<PlayerMovement>("PlayerMovement");
		_playerHealth = GetNode<PlayerHealth>("PlayerHealth");
		_playerAnimation = GetNode<PlayerAnimation>("PlayerAnimation");
	}

	public override void _PhysicsProcess(double delta)
	{
		float deltaTime = (float)delta;

		// Lower the cooldown each physics frame.
		if (_dashCooldownRemaining > 0.0f)
		{
			_dashCooldownRemaining -= deltaTime;
		}

		// Start the dash when the player presses E.
		if (
			Input.IsActionJustPressed("dash") &&
			!_isDashing &&
			_dashCooldownRemaining <= 0.0f
		)
		{
			StartDash();
		}

		if (_isDashing)
		{
			UpdateDash(deltaTime);
		}
		else
		{
			// Use normal movement when the player is not dashing.
			_movement.UpdateMovement(this, delta);

			// Remember the last horizontal direction.
			if (Velocity.X > 0.0f)
			{
				_lastDirection = 1.0f;
			}
			else if (Velocity.X < 0.0f)
			{
				_lastDirection = -1.0f;
			}
		}

		// Update the animation after movement or dashing.
		_playerAnimation.UpdateAnimation(this);
	}

	private void StartDash()
	{
		_isDashing = true;
		_dashTimeRemaining = DashDuration;
		_dashCooldownRemaining = DashCooldown;

		// Immediately apply the dash velocity.
		Velocity = new Vector2(
			_lastDirection * DashSpeed,
			0.0f
		);
	}

	private void UpdateDash(float deltaTime)
	{
		// Keep the player moving in the dash direction.
		Velocity = new Vector2(
			_lastDirection * DashSpeed,
			0.0f
		);

		MoveAndSlide();

		_dashTimeRemaining -= deltaTime;

		if (_dashTimeRemaining <= 0.0f)
		{
			_isDashing = false;

			// Stop the remaining dash speed.
			Velocity = new Vector2(0.0f, Velocity.Y);
		}
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
