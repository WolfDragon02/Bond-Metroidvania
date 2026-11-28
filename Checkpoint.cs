using Godot;

public partial class Checkpoint : Area2D
{
	public static Vector2 CurrentSpawnPosition { get; private set; }
	public static bool HasActiveCheckpoint { get; private set; }
	private Marker2D _spawnPoint = null!;
	private bool _playerInside = false;

	public override void _Ready()
	{
		_spawnPoint = GetNode<Marker2D>("SpawnPoint");

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _Process(double delta)
	{
		if (_playerInside &&
			Input.IsActionJustPressed("interact"))
		{
			CurrentSpawnPosition = _spawnPoint.GlobalPosition;
			HasActiveCheckpoint = true;
			GD.Print("Checkpoint activated!");
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is PlayerController)
		{
			_playerInside = true;
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is PlayerController)
		{
			_playerInside = false;
		}
	}
}
