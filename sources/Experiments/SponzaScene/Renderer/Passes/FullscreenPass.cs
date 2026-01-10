using Zenith.NET;
using Zenith.NET.Extensions.Slang;

namespace SponzaScene.Renderer.Passes;

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

        ExecuteBefore(commandBuffer, context);

        commandBuffer.SetPipeline(pipeline);

        if (resourceLayout is not null)
        {
            commandBuffer.SetResourceSet(EnsureResourceSet(resourceLayout, context), 0);
        }

        commandBuffer.Dispatch((context.Width + ThreadGroupSize - 1) / ThreadGroupSize, (context.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);

        ExecuteAfter(commandBuffer, context);
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

    protected virtual void ExecuteBefore(CommandBuffer commandBuffer, RenderContext context) { }

    protected virtual void ExecuteAfter(CommandBuffer commandBuffer, RenderContext context) { }
}
