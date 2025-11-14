using Zenith.NET;

namespace SponzaScene;

internal class MainView
{
    public MainView(GraphicsContext context)
    {
        Context = context;
    }

    public GraphicsContext Context { get; }

    public void Update(double delta)
    {
    }

    public void Render(double delta, FrameBuffer frameBuffer)
    {
        CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();

        commandBuffer.BindFrameBuffer(frameBuffer, ClearValues.Default);
        commandBuffer.Submit();
    }
}
