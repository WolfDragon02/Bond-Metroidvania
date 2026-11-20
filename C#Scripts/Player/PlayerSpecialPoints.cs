
using Godot;
using System;

public partial class PlayerSpecialPoints : Node
{
    [Export] public int MaxPoints { get; set; } = 100;

    public int CurrentPoints { get; private set; } = 0;

    public event Action<int, int> PointsChanged;

    public void GainPoints(int amount)
    {
        if (amount <= 0)
            return;

        CurrentPoints = Math.Clamp(
            CurrentPoints + amount, 0, MaxPoints);

        PointsChanged?.Invoke(CurrentPoints, MaxPoints);
    }

    public bool SpendPoints(int amount)
    {
        if (amount < 0 || CurrentPoints < amount)
            return false;

        CurrentPoints -= amount;

        PointsChanged?.Invoke(CurrentPoints, MaxPoints);
        return true;
    }
}
