namespace Zenith.NET.Views.WPF;

public class RenderEventArgs(double delta, FrameBuffer frameBuffer) : EventArgs
{
    public double Delta { get; } = delta;

    public FrameBuffer FrameBuffer { get; } = frameBuffer;
}
