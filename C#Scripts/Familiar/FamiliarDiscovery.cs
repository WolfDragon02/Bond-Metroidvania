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
		if (IsRevealed)
			return;
		
		IsRevealed = true;
		
		if (HiddenObject != null)
			HiddenObject.Visible = true;
			
		GD.Print("Discovery revealed!");
	}
	
	public void CompleteDiscovery()
	{
		if (!IsRevealed)
			return;
		
		IsCompleted = true;
		GD.Print("Discovery completed!");
	}
}
