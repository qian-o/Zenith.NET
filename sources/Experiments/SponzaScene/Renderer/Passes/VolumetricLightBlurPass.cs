using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class VolumetricLightBlurPass : RenderPass
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private ResourceSet? horizontalResourceSet;
    private ResourceSet? verticalResourceSet;

    private int iterations = 2;

    public VolumetricLightBlurPass() : base("Volumetric Light Blur Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(BlurConstants),
            StrideInBytes = (uint)sizeof(BlurConstants),
            Flags = BufferUsageFlags.Constant
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
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

        using Shader cs = App.Context.LoadShaderFromFile(GetShaderPath("VolumetricLightBlur"), "CSMain", ShaderStageFlags.Compute);

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

        uint dispatchX = (context.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint dispatchY = (context.Height + ThreadGroupSize - 1) / ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);

        for (int i = 0; i < iterations; i++)
        {
            commandBuffer.Upload(constantBuffer, 0, [new BlurConstants() { TexelSize = new(1.0f / context.Width, 0) }]);
            commandBuffer.SetResourceSet(horizontalResourceSet!, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            commandBuffer.Upload(constantBuffer, 0, [new BlurConstants() { TexelSize = new(0, 1.0f / context.Height) }]);
            commandBuffer.SetResourceSet(verticalResourceSet!, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);
        }
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderInt("Blur Iterations", ref iterations, 1, 4);

        ImGuiHelper.Image(context.VolumetricLightBlurred!);
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
            Resources =
            [
                constantBuffer,
                context.VolumetricLight!,
                context.Position!,
                context.VolumetricLightBlurred!,
                App.LinearSampler
            ]
        });

        verticalResourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.VolumetricLightBlurred!,
                context.Position!,
                context.VolumetricLight!,
                App.LinearSampler
            ]
        });
    }
}

file struct BlurConstants
{
    public Vector2 TexelSize;
}