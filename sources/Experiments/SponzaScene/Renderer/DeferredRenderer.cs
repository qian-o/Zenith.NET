using System.Numerics;
using Zenith.NET;

namespace SponzaScene.Renderer;

internal class DeferredRenderer : DisposableObject
{
    private readonly RenderContext context = new();
    private readonly List<RenderPass> renderPasses = [new GBufferPass()];

    public void Update(uint width, uint height, Matrix4x4 view, Matrix4x4 projection, Vector3 cameraPosition)
    {
        context.Initialize(width, height);

        context.View = view;
        context.Projection = projection;
        context.CameraPosition = cameraPosition;
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
