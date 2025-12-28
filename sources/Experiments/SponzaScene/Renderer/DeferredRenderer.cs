using SponzaScene.Models;
using Zenith.NET;

namespace SponzaScene.Renderer;

internal class DeferredRenderer : DisposableObject
{
    private readonly RenderContext context = new();
    private readonly List<RenderPass> renderPasses = [new GBufferPass(), new SSAOPass()];

    public void Update(uint width, uint height, CameraController camera)
    {
        context.Initialize(width, height);

        context.View = camera.View;
        context.Projection = camera.Projection;
        context.NearPlane = camera.NearPlane;
        context.FarPlane = camera.FarPlane;
        context.CameraPosition = camera.Position;
    }

    public void Render(CommandBuffer commandBuffer, FrameBuffer frameBuffer)
    {
        foreach (RenderPass renderPass in renderPasses)
        {
            if (renderPass.Enabled)
            {
                renderPass.DebugUI(context);
            }
        }

        commandBuffer.BeginDebugEvent("Deferred Rendering");

        foreach (RenderPass renderPass in renderPasses)
        {
            if (renderPass.Enabled)
            {
                renderPass.Execute(commandBuffer, context);
            }
        }

        commandBuffer.EndDebugEvent();
    }

    protected override void Destroy()
    {
        foreach (RenderPass renderPass in renderPasses)
        {
            renderPass.Dispose();
        }

        context.Dispose();
    }
}
