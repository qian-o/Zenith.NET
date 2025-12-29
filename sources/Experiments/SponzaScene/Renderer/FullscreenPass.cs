using Zenith.NET;
using Zenith.NET.Extensions.Slang;

namespace SponzaScene.Renderer;

internal abstract class FullscreenPass : RenderPass
{
    private const uint ThreadGroupSize = 16;

    private readonly ResourceLayout? resourceLayout;
    private readonly ComputePipeline pipeline;

    protected FullscreenPass(string name) : base(name)
    {
        resourceLayout = CreateResourceLayout();

        using Shader cs = App.Context.LoadShaderFromFile(GetShaderPath(ShaderName), "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = cs,
            ResourceLayouts = resourceLayout is null ? [] : [resourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
    }

    protected abstract string ShaderName { get; }

    protected sealed override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        UpdateResources(context);

        commandBuffer.BindPipeline(pipeline);

        if (resourceLayout is not null)
        {
            commandBuffer.PreprocessResourceSets([EnsureResourceSet(resourceLayout, context)]);
            commandBuffer.BindResourceSet(EnsureResourceSet(resourceLayout, context), 0);
        }

        commandBuffer.Dispatch((context.Width + ThreadGroupSize - 1) / ThreadGroupSize, (context.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        resourceLayout?.Dispose();

        base.Destroy();
    }

    protected abstract ResourceLayout? CreateResourceLayout();

    protected abstract ResourceSet EnsureResourceSet(ResourceLayout resourceLayout, RenderContext context);

    protected abstract void UpdateResources(RenderContext context);
}
