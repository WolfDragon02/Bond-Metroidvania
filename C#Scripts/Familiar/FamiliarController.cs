
using Godot;

public partial class FamiliarController : Node
{
	public enum FamiliarState
	{
		Following,
		Alerting,
		Guiding,
		Revealing,
		WaitingForInteraction
	}
	
	[Export] public Area2D DetectionArea { get; set; }
	[Export] public Label AlertLabel { get; set; }
	
	[Export] public float RevealDistance = 25f;
	[Export] public float InteractDistance = 55f;
	
	public FamiliarState State { get; private set; }
		= FamiliarState.Following;
	
	private FamiliarMain _main;
	private FamiliarMovement _movement;
	private Node2D _player;
	
	private FamiliarDiscovery _target;
	private FamiliarDiscovery _ignoredTarget;
	
	public override void _Ready()
	{
		_main = GetParent<FamiliarMain>();
		
		_movement = _main.GetNode<FamiliarMovement>(
			"FamiliarMovement");
		
		_player = GetTree()
			.GetFirstNodeInGroup("player") as Node2D;
			
		if (DetectionArea == null || AlertLabel == null)
		{
			GD.PushError("Detection references are missing!");
			SetPhysicsProcess(false);
			return;
		}
		
		AlertLabel.Visible = false;
		}
	
	public void UpdateDetection()
	{
		if (_player == null ||
			DetectionArea == null ||
			AlertLabel == null)
			return;
		
		switch (State)
		{
			case FamiliarState.Following:
				ScanForDiscoveries();
				break;

			case FamiliarState.Alerting:
				HandleAlertInput();
				break;

			case FamiliarState.Guiding:
				GuideToDiscovery();
				break;

			case FamiliarState.Revealing:
				RevealDiscovery();
				break;

			case FamiliarState.WaitingForInteraction:
				WaitForPlayer();
				break;
		}
	}

	private void ScanForDiscoveries()
	{
		if (_ignoredTarget != null &&
			(!IsInstanceValid(_ignoredTarget) ||
			 !DetectionArea.OverlapsArea(_ignoredTarget)))
		{
			_ignoredTarget = null;
		}

		FamiliarDiscovery nearest = null;
		float nearestDistance = float.MaxValue;

		foreach (Area2D area in DetectionArea.GetOverlappingAreas())
		{
			if (area is not FamiliarDiscovery discovery)
				continue;

			if (discovery.IsCompleted || discovery.IsRevealed)
				continue;

			if (discovery == _ignoredTarget)
				continue;

			float distance = _main.GlobalPosition.DistanceTo(
				discovery.GlobalPosition);

			if (distance < nearestDistance)
			{
				nearest = discovery;
				nearestDistance = distance;
			}
		}

		if (nearest != null)
		{
			_target = nearest;
			State = FamiliarState.Alerting;
			ShowAlert();
		}
	}

	private void ShowAlert()
	{
		AlertLabel.Visible = true;

		if (_target.Type ==
			FamiliarDiscovery.DiscoveryType.Secret)
		{
			AlertLabel.Text = "?";
			AlertLabel.Modulate = Colors.Cyan;
		}
		else
		{
			AlertLabel.Text = "!";
			AlertLabel.Modulate = Colors.Gold;
		}

		GD.Print("Press E to investigate!");
	}

	private void HandleAlertInput()
	{
		if (_target == null || !IsInstanceValid(_target))
		{
			ReturnToFollowing();
			return;
		}

		if (!DetectionArea.OverlapsArea(_target))
		{
			ReturnToFollowing();
			return;
		}

		if (Input.IsActionJustPressed("familiar_investigate"))
		{
			State = FamiliarState.Guiding;
			AlertLabel.Text = "→";

			_movement.SetGuideTarget(
				_target.GlobalPosition);
		}
		else if (Input.IsActionJustPressed("familiar_cancel"))
		{
			_ignoredTarget = _target;
			ReturnToFollowing();
		}
	}

	private void GuideToDiscovery()
	{
		if (_target == null || !IsInstanceValid(_target))
		{
			ReturnToFollowing();
			return;
		}

		if (Input.IsActionJustPressed("familiar_cancel"))
		{
			_ignoredTarget = _target;
			ReturnToFollowing();
			return;
		}

		float distance = _main.GlobalPosition.DistanceTo(
			_target.GlobalPosition);

		if (distance <= RevealDistance)
		{
			_movement.StopGuiding();
			State = FamiliarState.Revealing;
		}
	}

	private void RevealDiscovery()
	{
		_target.Reveal();

		AlertLabel.Text = "!";
		State = FamiliarState.WaitingForInteraction;
	}

	private void WaitForPlayer()
	{
		if (_target == null || !IsInstanceValid(_target))
		{
			ReturnToFollowing();
			return;
		}

		float distance = _player.GlobalPosition.DistanceTo(
			_target.GlobalPosition);

		if (distance <= InteractDistance &&
			Input.IsActionJustPressed("interact"))
		{
			_target.CompleteDiscovery();
			ReturnToFollowing();
		}
	}

	private void ReturnToFollowing()
	{
		_movement.StopGuiding();

		_target = null;
		AlertLabel.Visible = false;

		State = FamiliarState.Following;
	}
}
