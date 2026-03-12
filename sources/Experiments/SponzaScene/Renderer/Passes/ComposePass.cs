using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class ComposePass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceTable? resourceTable;

    private float aoStrength = 1.0f;
    private float bloomIntensity = 1.5f;
    private float volumetricIntensity = 2.5f;

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

    public override void Resize(uint width, uint height)
    {
        resourceTable?.Dispose();
        resourceTable = null;
    }

    protected override ResourceLayout? CreateResourceLayout()
    {
        return App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });
    }

    protected override ResourceTable EnsureResourceTable(ResourceLayout resourceLayout, RenderContext context)
    {
        return resourceTable ??= App.Context.CreateResourceTable(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.LitColor!,
                context.GTAOBlurred!,
                context.VerticalBloom!,
                context.VolumetricLightBlurred!,
                context.FinalColor!,
                App.PointSampler
            ]
        });
    }

    protected override void UpdateResources(RenderContext context)
    {
        constantBuffer.Upload([new ComposeConstants
        {
            AOStrength = aoStrength,
            BloomIntensity = bloomIntensity,
            VolumetricIntensity = volumetricIntensity
        }], 0);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderFloat("AO Strength", ref aoStrength, 0.0f, 2.0f);
        ImGui.SliderFloat("Bloom Intensity", ref bloomIntensity, 0.0f, 2.0f);
        ImGui.SliderFloat("Volumetric Intensity", ref volumetricIntensity, 0.0f, 5.0f);

        ImGuiHelper.Image(context.FinalColor!);
    }

    protected override void Destroy()
    {
        resourceTable?.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }
}

file struct ComposeConstants
{
    public float AOStrength;

    public float BloomIntensity;

    public float VolumetricIntensity;

    private float padding0;
}