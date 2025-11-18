namespace Zenith.NET.Views.WPF;

public class RenderEventArgs(double deltaTime, double totalTime, FrameBuffer frameBuffer) : EventArgs
{
    public double DeltaTime { get; } = deltaTime;

    public double TotalTime { get; } = totalTime;

    public FrameBuffer FrameBuffer { get; } = frameBuffer;
}
