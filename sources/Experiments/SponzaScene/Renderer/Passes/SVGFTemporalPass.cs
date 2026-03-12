using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class SVGFTemporalPass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceTable? resourceTable;
    private Matrix4x4 prevViewProjection;

    private float colorBoxSigma = 2.0f;
    private float normalThreshold = 0.95f;
    private float depthThreshold = 0.02f;
    private int maxHistoryLength = 32;

    public SVGFTemporalPass() : base("SVGF Temporal Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(TemporalConstants),
            StrideInBytes = (uint)sizeof(TemporalConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });
    }

    protected override string ShaderName => "SVGFTemporal";

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
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
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
                context.RTGI!,
                context.Position!,
                context.Normal!,
                context.HistoryPosition!,
                context.HistoryNormal!,
                context.RTGIHistoryAccumulated!,
                context.RTGIHistoryMoments!,
                context.RTGIAccumulated!,
                context.RTGIMoments!,
                context.SVGFPingPong!,
                App.LinearSampler
            ]
        });
    }

    protected override void UpdateResources(RenderContext context)
    {
        constantBuffer.Upload([new TemporalConstants
        {
            PrevViewProjection = prevViewProjection,
            ViewportSize = new Vector2(context.Width, context.Height),
            ColorBoxSigma = colorBoxSigma,
            NormalThreshold = normalThreshold,
            DepthThreshold = depthThreshold,
            MaxHistoryLength = maxHistoryLength
        }], 0);
    }

    protected override void ExecuteBefore(CommandBuffer commandBuffer, RenderContext context)
    {
        commandBuffer.CopyTexture(context.Position!,
                                  default,
                                  default,
                                  context.HistoryPosition!,
                                  default,
                                  default,
                                  new() { Width = context.Width, Height = context.Height, Depth = 1 });

        commandBuffer.CopyTexture(context.Normal!,
                                  default,
                                  default,
                                  context.HistoryNormal!,
                                  default,
                                  default,
                                  new() { Width = context.Width, Height = context.Height, Depth = 1 });

        commandBuffer.CopyTexture(context.RTGIAccumulated!,
                                  default,
                                  default,
                                  context.RTGIHistoryAccumulated!,
                                  default,
                                  default,
                                  new() { Width = context.Width, Height = context.Height, Depth = 1 });

        commandBuffer.CopyTexture(context.RTGIMoments!,
                                  default,
                                  default,
                                  context.RTGIHistoryMoments!,
                                  default,
                                  default,
                                  new() { Width = context.Width, Height = context.Height, Depth = 1 });

        prevViewProjection = context.View * context.Projection;
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderFloat("Color Box Sigma", ref colorBoxSigma, 0.5f, 5.0f);
        ImGui.SliderFloat("Normal Threshold", ref normalThreshold, 0.8f, 1.0f);
        ImGui.SliderFloat("Depth Threshold", ref depthThreshold, 0.001f, 0.1f);
        ImGui.SliderInt("Max History Length", ref maxHistoryLength, 4, 64);

        ImGuiHelper.Image(context.SVGFPingPong!);
    }

    protected override void Destroy()
    {
        resourceTable?.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 96)]
file struct TemporalConstants
{
    [FieldOffset(0)]
    public Matrix4x4 PrevViewProjection;

    [FieldOffset(64)]
    public Vector2 ViewportSize;

    [FieldOffset(72)]
    public float ColorBoxSigma;

    [FieldOffset(76)]
    public float NormalThreshold;

    [FieldOffset(80)]
    public float DepthThreshold;

    [FieldOffset(84)]
    public int MaxHistoryLength;
}
