namespace Zenith.NET.Views;

public class RenderEventArgs(double deltaSeconds, double totalSeconds, CommandBuffer commandBuffer, Texture drawable) : EventArgs
{
    public double DeltaSeconds { get; } = deltaSeconds;

    public double TotalSeconds { get; } = totalSeconds;

    public CommandBuffer CommandBuffer { get; } = commandBuffer;

    public Texture Drawable { get; } = drawable;
}
