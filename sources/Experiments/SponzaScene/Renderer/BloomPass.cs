using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class BloomPass : RenderPass
{
    private const uint ThreadGroupSize = 8;

    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private ResourceSet? horizontalResourceSet;
    private ResourceSet? verticalResourceSet;

    private int iterations = 4;

    public BloomPass() : base("Bloom Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(BloomConstants),
            StrideInBytes = (uint)sizeof(BloomConstants),
            Flags = BufferUsageFlags.Constant
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings =
            [
                new() { Type = ResourceType.ConstantBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Sampler, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Compute }
            ]
        });

        using Shader cs = App.Context.LoadShaderFromFile(GetShaderPath("Bloom"), "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = cs,
            ResourceLayouts = [resourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
    }

    public override void Execute(CommandBuffer commandBuffer, RenderContext context)
    {
        EnsureResourceSets(context);

        commandBuffer.CopyTexture(context.Emissive!,
                                  default,
                                  default,
                                  context.VerticalBloom!,
                                  default,
                                  default,
                                  new() { Width = context.Width, Height = context.Height, Depth = 1 });

        uint dispatchX = (context.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint dispatchY = (context.Height + ThreadGroupSize - 1) / ThreadGroupSize;

        commandBuffer.BindPipeline(pipeline);

        for (int i = 0; i < iterations; i++)
        {
            commandBuffer.Upload(constantBuffer, 0, [new BloomConstants() { TexelSize = new(1.0f / context.Width, 0) }]);
            commandBuffer.BindResourceSet(horizontalResourceSet!, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            commandBuffer.Upload(constantBuffer, 0, [new BloomConstants() { TexelSize = new(0, 1.0f / context.Height) }]);
            commandBuffer.BindResourceSet(verticalResourceSet!, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);
        }
    }

    public override void DebugUI(RenderContext context)
    {
        ImGui.SliderInt("Blur Iterations", ref iterations, 1, 8);

        Vector2 size = new(ImGui.GetContentRegionAvail().X);
        size = size with { Y = size.X * context.Height / context.Width };

        ImGui.Image(App.Binding(context.VerticalBloom!), size);
    }

    public override void Resize(uint width, uint height)
    {
        horizontalResourceSet?.Dispose();
        horizontalResourceSet = null;

        verticalResourceSet?.Dispose();
        verticalResourceSet = null;
    }

    protected override void Destroy()
    {
        verticalResourceSet?.Dispose();
        horizontalResourceSet?.Dispose();

        pipeline.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();
    }

    private void EnsureResourceSets(RenderContext context)
    {
        horizontalResourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources = [constantBuffer, context.VerticalBloom!, context.HorizontalBloom!, App.LinearSampler]
        });

        verticalResourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources = [constantBuffer, context.HorizontalBloom!, context.VerticalBloom!, App.LinearSampler]
        });
    }

    private struct BloomConstants
    {
        public Vector2 TexelSize;
    }
}
