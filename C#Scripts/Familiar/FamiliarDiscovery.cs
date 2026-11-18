using Godot;
public partial class FamiliarDiscovery : Area2D
{
	public enum DiscoveryType
	{
		Secret,
		Treasure
		}
	
	[Export]
	public DiscoveryType Type { get; set; }
		= DiscoveryType.Secret;
	
	[Export]
	public Node2D HiddenObject { get; set; }
	
	public bool IsRevealed { get; private set; }
	public bool IsCompleted { get; private set; }
	
	public override void _Ready()
	{
		if (HiddenObject != null)
			HiddenObject.Visible = false;
	}
	
	public void Reveal()
	{
		GD.Print("FamiliarDiscovery.Reveal() entered!");
		
		if (IsRevealed)
		{
			GD.Print("Discovery already revealed.");
			return;
		}
		
		if (HiddenObject == null) 
		{
			GD.PushError(
				"ERROR: HiddenObject is not assigned!"
			);
			return;
		}
		
		GD.Print("Hidden object: ", HiddenObject.Name);
		GD.Print("Before reveal: ", HiddenObject.Visible);
		
		HiddenObject.Show();
		
		GD.Print("After reveal: ", HiddenObject.Visible);
		GD.Print("Visible in tree: ", 
			HiddenObject.IsVisibleInTree());
		
		IsRevealed = true;
		
		GD.Print("Reveal() called!");
		if (HiddenObject != null)
		{
			HiddenObject.Visible = true;
			GD.Print("Hidden object made visible!");
		}
		else
		{
			GD.PrintErr("HiddenObject is not assigned!");
		}
	}
	
	public void CompleteDiscovery()
	{
		if (!IsRevealed)
			return;
		
		IsCompleted = true;
		GD.Print("Discovery completed!");
	}
}
