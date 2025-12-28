using Zenith.NET;

namespace SponzaScene.Renderer;

internal unsafe class ComposePass() : FullscreenPass("ComposePass")
{
    private ResourceSet? resourceSet;

    protected override string ShaderName => "Compose";

    protected override Output Output => RenderContext.ComposeOutput;

    protected override ResourceLayout? CreateResourceLayout()
    {
        return App.Context.CreateResourceLayout(new()
        {
            Bindings =
            [
                new() { Type = ResourceType.Texture, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            ]
        });
    }

    protected override ResourceSet EnsureResourceSet(ResourceLayout resourceLayout, RenderContext context)
    {
        return resourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                context.Albedo!,
                context.SSAOBlurred!,
                App.PointSampler
            ]
        });
    }

    protected override (FrameBuffer? FrameBuffer, ClearValue ClearValue) GetTarget(RenderContext context)
    {
        return (context.ComposeFrameBuffer, ClearValues.Default);
    }

    protected override void UpdateResources(RenderContext context)
    {
    }

    public override void DebugUI(RenderContext context)
    {
    }

    public override void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();

        base.Destroy();
    }
}