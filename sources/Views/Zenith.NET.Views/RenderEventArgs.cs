namespace Zenith.NET.Views;

public class RenderEventArgs(double deltaTime, double totalTime, FrameBuffer frameBuffer) : EventArgs
{
    public double DeltaTime { get; } = deltaTime;

    public double TotalTime { get; } = totalTime;

    public FrameBuffer FrameBuffer { get; } = frameBuffer;
}
