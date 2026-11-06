using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private readonly Buffer constants;
    private readonly GraphicsPipeline toneMappingPipeline;

    public OutputPass()
    {
        constants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = 256,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang"), "FullscreenVS"));
        using Shader toneMappingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang"), "ToneMappingFS"));

        toneMappingPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = toneMappingFragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.B8G8R8A8UNorm],
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        CreateTextures();
    }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture input, uint frameIndex)
    {
        OutputConstants toneMappingConstants = new()
        {
            Input = input.SampledHandle,
            FrameIndex = frameIndex
        };

        constants.Upload(0, new()
        {
            Pointer = (nint)(&toneMappingConstants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        Draw(commandBuffer, toneMappingPipeline, Color);
    }

    public void Resize()
    {
        DestroyTextures();
        CreateTextures();
    }

    protected override void Destroy()
    {
        DestroyTextures();
        toneMappingPipeline?.Dispose();
        constants?.Dispose();
    }

    private void CreateTextures()
    {
        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.Sampled
        });
    }

    private void DestroyTextures()
    {
        Color?.Dispose();
        Color = null!;
    }

    private void Draw(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Texture output)
    {
        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(output)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(output, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct OutputConstants
{
    [FieldOffset(0)]
    public ResourceHandle Input;

    [FieldOffset(16)]
    public uint FrameIndex;
}
