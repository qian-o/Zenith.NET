namespace Zenith.NET.Views;

public class RenderEventArgs(double deltaSeconds, double totalSeconds, Texture target) : EventArgs
{
    public double DeltaSeconds { get; } = deltaSeconds;

    public double TotalSeconds { get; } = totalSeconds;

    public Texture Target { get; } = target;
}
