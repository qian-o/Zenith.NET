namespace Zenith.NET.Views;

public class RenderEventArgs(double deltaSeconds, double totalSeconds, Texture drawable) : EventArgs
{
    public double DeltaSeconds { get; } = deltaSeconds;

    public double TotalSeconds { get; } = totalSeconds;

    public Texture Drawable { get; } = drawable;
}
