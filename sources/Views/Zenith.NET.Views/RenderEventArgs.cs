namespace Zenith.NET.Views;

public class RenderEventArgs(double deltaSeconds, double totalSeconds, FrameBuffer frameBuffer) : EventArgs
{
    public double DeltaSeconds { get; } = deltaSeconds;

    public double TotalSeconds { get; } = totalSeconds;

    public FrameBuffer FrameBuffer { get; } = frameBuffer;
}
