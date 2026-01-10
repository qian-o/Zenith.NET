using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class SVGFTemporalPass : RenderPass
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private ResourceSet? resourceSet;
    private Matrix4x4 prevViewProjection;

    private float colorBoxSigma = 3.0f;
    private float normalThreshold = 0.9f;
    private float depthThreshold = 0.05f;
    private int maxHistoryLength = 32;

    public SVGFTemporalPass() : base("SVGF Temporal Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(TemporalConstants),
            StrideInBytes = (uint)sizeof(TemporalConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
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

        using Shader cs = App.Context.LoadShaderFromFile(GetShaderPath("SVGFTemporal"), "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = cs,
            ResourceLayouts = [resourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
    }

    public override void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;

        prevViewProjection = Matrix4x4.Identity;
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        EnsureResourceSet(context);

        constantBuffer.Upload([new TemporalConstants
        {
            PrevViewProjection = prevViewProjection,
            ViewportSize = new Vector2(context.Width, context.Height),
            ColorBoxSigma = colorBoxSigma,
            NormalThreshold = normalThreshold,
            DepthThreshold = depthThreshold,
            MaxHistoryLength = maxHistoryLength
        }], 0);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceSet(resourceSet!, 0);

        uint dispatchX = (context.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint dispatchY = (context.Height + ThreadGroupSize - 1) / ThreadGroupSize;
        commandBuffer.Dispatch(dispatchX, dispatchY, 1);

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

        prevViewProjection = context.View * context.Projection;
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderFloat("Color Box Sigma", ref colorBoxSigma, 0.1f, 5.0f);
        ImGui.SliderFloat("Normal Threshold", ref normalThreshold, 0.5f, 1.0f);
        ImGui.SliderFloat("Depth Threshold", ref depthThreshold, 0.01f, 0.5f);
        ImGui.SliderInt("Max History Length", ref maxHistoryLength, 4, 64);

        ImGuiHelpers.Image(context.SVGFPingPong!);
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();
        pipeline.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private void EnsureResourceSet(RenderContext context)
    {
        resourceSet ??= App.Context.CreateResourceSet(new()
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
                context.RTGIAccumulated!,
                context.RTGIMoments!,
                context.SVGFPingPong!,
                App.LinearSampler
            ]
        });
    }

    private struct TemporalConstants
    {
        public Matrix4x4 PrevViewProjection;

        public Vector2 ViewportSize;

        public float ColorBoxSigma;

        public float NormalThreshold;

        public float DepthThreshold;

        public int MaxHistoryLength;
    }
}
