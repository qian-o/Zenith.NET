using System.Runtime.InteropServices;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private readonly Buffer constantBuffer;
    private readonly GraphicsPipeline pipeline;

    public OutputPass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(OutputConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang");

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FullscreenVS"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ToneMappingFS"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
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

        CreateTextures();
    }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture input, uint frameIndex)
    {
        OutputConstants constants = new()
        {
            Width = Color.Desc.Width,
            FrameIndex = frameIndex,
            Input = input.SampledHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Resize()
    {
        if (Color.Desc.Width == App.PhysicalWidth && Color.Desc.Height == App.PhysicalHeight)
        {
            return;
        }

        DestroyTextures();
        CreateTextures();
    }

    protected override void Destroy()
    {
        DestroyTextures();

        pipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void CreateTextures()
    {
        Color = App.Context.CreateTexture(TextureDesc.ColorAttachment(App.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.UpscalingInput
        });
    }

    private void DestroyTextures()
    {
        Color?.Dispose();
        Color = null!;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct OutputConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint FrameIndex;

    [FieldOffset(16)]
    public ResourceHandle Input;
}
