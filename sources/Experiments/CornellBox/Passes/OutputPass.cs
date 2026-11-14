using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private readonly Buffer constantBuffer;
    private readonly GraphicsPipeline toneMappingPipeline;

    public OutputPass()
    {
        constantBuffer = App.Context.CreateBuffer(BufferDesc.Constant(256));

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
                ColorFormats = [App.ColorFormat],
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        CreateOutput();
    }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture input, uint frameIndex)
    {
        OutputConstants toneMappingConstants = new()
        {
            Input = input.SampledHandle,
            FrameIndex = frameIndex
        };

        commandBuffer.Upload(constantBuffer, 0, new()
        {
            Pointer = (nint)(&toneMappingConstants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });
        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.FragmentShading);

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(toneMappingPipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Resize()
    {
        DestroyOutput();
        CreateOutput();
    }

    protected override void Destroy()
    {
        DestroyOutput();
        toneMappingPipeline.Dispose();
        constantBuffer.Dispose();
    }

    private void CreateOutput()
    {
        Color = App.Context.CreateTexture(TextureDesc.ColorAttachment(App.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1));
    }

    private void DestroyOutput()
    {
        Color?.Dispose();
        Color = null!;
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
