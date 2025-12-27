using SponzaScene.Renderer;
using Zenith.NET;

namespace SponzaScene;

internal class MainView : DisposableObject
{
    private readonly DeferredRenderer renderer = new();

    public void Update(uint width, uint height)
    {
        renderer.Update(width, height, default, default, default);
    }

    public void Render()
    {
        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        renderer.Render(commandBuffer);

        commandBuffer.Submit();
    }

    protected override void Destroy()
    {
        renderer.Dispose();
    }
}
