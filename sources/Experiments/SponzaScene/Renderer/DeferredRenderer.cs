using Hexa.NET.ImGui;
using SponzaScene.Models;
using SponzaScene.Renderer.Passes;
using Zenith.NET;

namespace SponzaScene.Renderer;

internal class DeferredRenderer : DisposableObject
{
    private readonly RenderPass[] passes;
    private readonly RenderContext context;

    public DeferredRenderer()
    {
        if (App.Context.Capabilities.RayTracingSupported)
        {
            passes =
            [
                new GBufferPass(),
                new CSMPass(),
                new RTGIPass(),
                new GTAOPass(),
                new GTAOBlurPass(),
                new VolumetricLightPass(),
                new VolumetricLightBlurPass(),
                new BloomPass(),
                new LightingPass(),
                new ComposePass()
            ];
        }
        else
        {
            passes =
            [
                new GBufferPass(),
                new CSMPass(),
                new GTAOPass(),
                new GTAOBlurPass(),
                new VolumetricLightPass(),
                new VolumetricLightBlurPass(),
                new BloomPass(),
                new LightingPass(),
                new ComposePass()
            ];
        }

        context = new();
    }

    public void Update(uint width, uint height, CameraController camera)
    {
        if (context.Width != width || context.Height != height)
        {
            context.Initialize(width, height);

            foreach (RenderPass pass in passes)
            {
                pass.Resize(width, height);
            }
        }

        context.View = camera.View;
        context.Projection = camera.Projection;
        context.NearPlane = camera.NearPlane;
        context.FarPlane = camera.FarPlane;
        context.CameraPosition = camera.Position;
        context.Fov = camera.Fov;
        context.AspectRatio = camera.AspectRatio;
    }

    public void Render()
    {
        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        commandBuffer.BeginDebugEvent("Deferred Rendering");

        foreach (RenderPass pass in passes)
        {
            pass.Execute(commandBuffer, context);
        }

        commandBuffer.EndDebugEvent();

        commandBuffer.Submit(true);
    }

    public void UI()
    {
        ImGui.GetBackgroundDrawList().AddImage(App.Binding(context.FinalColor!), default, new(context.Width, context.Height));

        if (ImGui.Begin("Deferred Renderer"))
        {
            App.Sponza.UI();

            foreach (RenderPass pass in passes)
            {
                bool opened = ImGui.CollapsingHeader(pass.Name);

                ImGui.SameLine(ImGui.GetWindowWidth() - 80);
                ImGui.Text($"{pass.GpuTime:F2} ms");

                if (opened)
                {
                    pass.DebugUI(context);
                }
            }
        }
        ImGui.End();
    }

    protected override void Destroy()
    {
        foreach (RenderPass pass in passes)
        {
            pass.Dispose();
        }

        context.Dispose();
    }
}