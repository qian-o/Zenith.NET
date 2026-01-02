using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class SSGIBlurPass : RenderPass
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private ResourceSet? resourceSetA; // Read from SSGIHistory
    private ResourceSet? resourceSetB; // Read from SSGI

    private int blurSize = 12;
    private float normalSigma = 0.2f;
    private float depthSigma = 2.0f;

    public SSGIBlurPass() : base("SSGI Blur Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SSGIBlurConstants),
            StrideInBytes = (uint)sizeof(SSGIBlurConstants),
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
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        using Shader cs = App.Context.LoadShaderFromFile(GetShaderPath("SSGIBlur"), "CSMain", ShaderStageFlags.Compute);

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
        resourceSetA?.Dispose();
        resourceSetA = null;
        resourceSetB?.Dispose();
        resourceSetB = null;
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        EnsureResourceSets(context);

        constantBuffer.Upload([new SSGIBlurConstants
        {
            TexelSize = new Vector2(1.0f / context.Width, 1.0f / context.Height),
            BlurSize = blurSize,
            NormalSigma = normalSigma,
            DepthSigma = depthSigma
        }], 0);

        commandBuffer.SetPipeline(pipeline);
        
        // Use the resource set that reads from the texture that was just written by SSGIPass
        // SSGIPass swaps after writing, so if FrameIndex is even, it just wrote to SSGI (before swap)
        // After swap: useSetA becomes false, meaning it will read SSGI next frame
        // So blur should read from the opposite of what SSGIPass will read next
        bool readFromSSGI = (context.FrameIndex % 2 == 1);
        commandBuffer.SetResourceSet(readFromSSGI ? resourceSetB! : resourceSetA!, 0);
        
        commandBuffer.Dispatch((context.Width + ThreadGroupSize - 1) / ThreadGroupSize,
                               (context.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderInt("Blur Size", ref blurSize, 1, 24);
        ImGui.SliderFloat("Normal Sigma", ref normalSigma, 0.01f, 1.0f);
        ImGui.SliderFloat("Depth Sigma", ref depthSigma, 0.1f, 5.0f);

        Vector2 size = new(ImGui.GetContentRegionAvail().X);
        size = size with { Y = size.X * context.Height / context.Width };

        ImGui.Image(App.Binding(context.SSGIBlurred!), size);
    }

    protected override void Destroy()
    {
        resourceSetB?.Dispose();
        resourceSetA?.Dispose();
        pipeline.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private void EnsureResourceSets(RenderContext context)
    {
        // Set A: Read from SSGIHistory (when SSGIPass wrote to SSGIHistory)
        resourceSetA ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.SSGIHistory!,
                context.Position!,
                context.Normal!,
                context.SSGIBlurred!,
                App.PointSampler
            ]
        });

        // Set B: Read from SSGI (when SSGIPass wrote to SSGI)
        resourceSetB ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.SSGI!,
                context.Position!,
                context.Normal!,
                context.SSGIBlurred!,
                App.PointSampler
            ]
        });
    }

    private struct SSGIBlurConstants
    {
        public Vector2 TexelSize;

        public int BlurSize;

        public float NormalSigma;

        public float DepthSigma;
    }
}
