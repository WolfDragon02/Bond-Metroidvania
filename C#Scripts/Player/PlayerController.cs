using Godot;
using System.Threading.Tasks;

public partial class PlayerController : Entity
{
	private PlayerMovement _movement = null!;
	private PlayerHealth _playerHealth = null!;
	private PlayerAnimation _playerAnimation = null!;

	private Vector2 _startingSpawnPosition;
	private bool _isDead;

	public override void _Ready()
	{
		base._Ready();

		_movement = GetNode<PlayerMovement>("PlayerMovement");
		_playerHealth = GetNode<PlayerHealth>("PlayerHealth");
		_playerAnimation =
			GetNode<PlayerAnimation>("PlayerAnimation");

		_startingSpawnPosition = GlobalPosition;
		_playerHealth.Died += OnPlayerDied;

		// After the level reloads, place the player at the
		// last activated checkpoint.
		if (Checkpoint.HasActiveCheckpoint)
		{
			GlobalPosition = Checkpoint.CurrentSpawnPosition;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Do not allow movement while the player is dead.
		if (_isDead)
		{
			Velocity = Vector2.Zero;
			return;
		}

		_movement.UpdateMovement(this, delta);
		_playerAnimation.UpdateAnimation(this);
	}

	public override void _Process(double delta)
	{
		if (!_isDead &&
			Input.IsActionJustPressed("test_damage"))
		{
			_playerHealth.TakeDamage(10);
		}
	}

	private async void OnPlayerDied()
	{
		if (_isDead)
		{
			return;
		}

		_isDead = true;
		Velocity = Vector2.Zero;

		GD.Print("Player died. Reloading level...");

		// Wait one second before respawning.
		await ToSignal(
			GetTree().CreateTimer(1.0),
			SceneTreeTimer.SignalName.Timeout);

		// Reload the level, which resets enemies and hazards.
		GetTree().ReloadCurrentScene();
	}

	public override void _ExitTree()
	{
		if (_playerHealth != null)
		{
			_playerHealth.Died -= OnPlayerDied;
		}

		base._ExitTree();
	}
}
