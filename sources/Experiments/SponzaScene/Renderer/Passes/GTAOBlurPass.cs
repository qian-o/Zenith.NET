using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class GTAOBlurPass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceTable? resourceTable;

    private int blurSize = 4;

    public GTAOBlurPass() : base("GTAO Blur Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(BlurConstants),
            StrideInBytes = (uint)sizeof(BlurConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });
    }

    protected override string ShaderName => "GTAOBlur";

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
                context.Position!,
                context.GTAO!,
                context.GTAOBlurred!,
                App.PointSampler
            ]
        });
    }

    protected override void UpdateResources(RenderContext context)
    {
        constantBuffer.Upload([new BlurConstants
        {
            TexelSize = new(1.0f / context.Width, 1.0f / context.Height),
            BlurSize = blurSize
        }], 0);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderInt("Blur Size", ref blurSize, 1, 8);

        ImGuiHelper.Image(context.GTAOBlurred!);
    }

    protected override void Destroy()
    {
        resourceTable?.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
file struct BlurConstants
{
    [FieldOffset(0)]
    public Vector2 TexelSize;

    [FieldOffset(8)]
    public int BlurSize;
}