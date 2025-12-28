using Hexa.NET.ImGui;
using SponzaScene.Models;
using Zenith.NET;

namespace SponzaScene.Renderer;

internal class DeferredRenderer : DisposableObject
{
    private readonly RenderContext context = new();
    private readonly List<RenderPass> renderPasses =
    [
        new GBufferPass(),
        new SSAOPass(),
        new SSAOBlurPass(),
        new ComposePass()
    ];

    public void Update(uint width, uint height, CameraController camera)
    {
        if (context.Width != width || context.Height != height)
        {
            context.Initialize(width, height);

            foreach (RenderPass renderPass in renderPasses)
            {
                renderPass.Resize(width, height);
            }
        }

        context.View = camera.View;
        context.Projection = camera.Projection;
        context.NearPlane = camera.NearPlane;
        context.FarPlane = camera.FarPlane;
        context.CameraPosition = camera.Position;
    }

    public void Render()
    {
        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        commandBuffer.BeginDebugEvent("Deferred Rendering");

        foreach (RenderPass renderPass in renderPasses)
        {
            if (renderPass.Enabled)
            {
                renderPass.Execute(commandBuffer, context);
            }
        }

        commandBuffer.EndDebugEvent();

        commandBuffer.Submit();

        foreach (RenderPass renderPass in renderPasses)
        {
            if (renderPass.Enabled)
            {
                renderPass.DebugUI(context);
            }
        }

        ImGui.GetBackgroundDrawList().AddImage(App.Binding(context.FinalColor!), default, new(context.Width, context.Height));
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
