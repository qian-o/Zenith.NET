using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;

namespace SponzaScene.Renderer;

internal class SSAOPass() : FullscreenPass("SSAOPass")
{
    protected override string ShaderName => "SSAO";

    protected override Output Output => RenderContext.SSAOOutput;

    protected override ResourceLayout? CreateResourceLayout()
    {
        return null;
    }

    protected override ResourceSet EnsureResourceSet(ResourceLayout resourceLayout, RenderContext context)
    {
        throw new NotImplementedException();
    }

    protected override (FrameBuffer? FrameBuffer, ClearValue ClearValue) GetTarget(RenderContext context)
    {
        return (context.SSAOFrameBuffer, ClearValues.Default);
    }

    protected override void UpdateResources(RenderContext context)
    {
    }

    public override void DebugUI(RenderContext context)
    {
        if (ImGui.Begin("SSAO"))
        {
            Vector2 size = new(ImGui.GetContentRegionAvail().X);
            size = size with { Y = size.X * context.Height / context.Width };

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((ImGui.GetContentRegionAvail().Y - size.Y) / 2));
            ImGui.Image(App.Binding(context.SSAO!), size);
        }
        ImGui.End();
    }
}
