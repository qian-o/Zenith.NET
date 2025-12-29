using Hexa.NET.ImGui;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class ComposePass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceSet? resourceSet;

    private float aoStrength = 1.0f;
    private float bloomIntensity = 1.0f;

    public ComposePass() : base("Compose Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(ComposeConstants),
            StrideInBytes = (uint)sizeof(ComposeConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });
    }

    protected override string ShaderName => "Compose";

    protected override Output Output => RenderContext.ComposeOutput;

    protected override ResourceLayout? CreateResourceLayout()
    {
        return App.Context.CreateResourceLayout(new()
        {
            Bindings =
            [
                new() { Type = ResourceType.ConstantBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 2, Count = 1, StageFlags = ShaderStageFlags.Pixel },
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
                constantBuffer,
                context.LitColor!,
                context.SSAOBlurred!,
                context.VerticalBloom!,
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
        constantBuffer.Upload([new ComposeConstants
        {
            AOStrength = aoStrength,
            BloomIntensity = bloomIntensity
        }], 0);
    }

    public override void DebugUI(RenderContext context)
    {
        ImGui.SliderFloat("AO Strength", ref aoStrength, 0.0f, 2.0f);
        ImGui.SliderFloat("Bloom Intensity", ref bloomIntensity, 0.0f, 2.0f);
    }

    public override void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private struct ComposeConstants
    {
        public float AOStrength;

        public float BloomIntensity;
    }
}