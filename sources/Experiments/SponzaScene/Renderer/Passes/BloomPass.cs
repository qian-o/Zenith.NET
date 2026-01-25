using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class BloomPass : RenderPass
{
    private const uint ThreadGroupSize = 16;

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
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
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

    public override void Resize(uint width, uint height)
    {
        horizontalResourceSet?.Dispose();
        horizontalResourceSet = null;

        verticalResourceSet?.Dispose();
        verticalResourceSet = null;
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
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

        commandBuffer.SetPipeline(pipeline);

        for (int i = 0; i < iterations; i++)
        {
            commandBuffer.Upload(constantBuffer, 0, [new BloomConstants() { TexelSize = new(1.0f / context.Width, 0) }]);

            commandBuffer.SetResourceSet(horizontalResourceSet!, 0);

            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            commandBuffer.Upload(constantBuffer, 0, [new BloomConstants() { TexelSize = new(0, 1.0f / context.Height) }]);

            commandBuffer.SetResourceSet(verticalResourceSet!, 0);

            commandBuffer.Dispatch(dispatchX, dispatchY, 1);
        }
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderInt("Blur Iterations", ref iterations, 1, 8);

        ImGuiHelpers.Image(context.VerticalBloom!);
    }

    protected override void Destroy()
    {
        verticalResourceSet?.Dispose();
        horizontalResourceSet?.Dispose();

        pipeline.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
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
}

file struct BloomConstants
{
    public Vector2 TexelSize;
}
