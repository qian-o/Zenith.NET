using Zenith.NET;
using Zenith.NET.Extensions.Slang;

namespace SponzaScene.Renderer;

internal abstract class FullscreenPass : RenderPass
{
    private readonly ResourceLayout? resourceLayout;
    private readonly GraphicsPipeline pipeline;

    protected FullscreenPass(string name) : base(name)
    {
        resourceLayout = CreateResourceLayout();

        using Shader vs = App.Context.LoadShaderFromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Common", "Fullscreen.slang"), "VSMain", ShaderStageFlags.Vertex);
        using Shader ps = App.Context.LoadShaderFromFile(GetShaderPath(ShaderName), "PSMain", ShaderStageFlags.Pixel);

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullNone,
                DepthStencilState = DepthStencilStates.None,
                BlendState = BlendStates.Opaque
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayouts = [],
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = Output
        });
    }

    protected abstract string ShaderName { get; }

    protected abstract Output Output { get; }

    public sealed override void Execute(CommandBuffer commandBuffer, RenderContext context)
    {
        (FrameBuffer? frameBuffer, ClearValue clearValue) = GetTarget(context);

        if (frameBuffer is null)
        {
            return;
        }

        UpdateResources(context);

        commandBuffer.BindFrameBuffer(frameBuffer, clearValue);
        commandBuffer.BindPipeline(pipeline);

        if (resourceLayout is not null)
        {
            commandBuffer.BindResourceSet(EnsureResourceSet(resourceLayout, context), 0);
        }

        commandBuffer.Draw(3, 1, 0, 0);
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        resourceLayout?.Dispose();
    }

    protected abstract ResourceLayout? CreateResourceLayout();

    protected abstract ResourceSet EnsureResourceSet(ResourceLayout resourceLayout, RenderContext context);

    protected abstract void UpdateResources(RenderContext context);

    protected abstract (FrameBuffer? FrameBuffer, ClearValue ClearValue) GetTarget(RenderContext context);
}
