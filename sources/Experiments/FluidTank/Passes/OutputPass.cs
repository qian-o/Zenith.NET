using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private readonly Buffer constants;
    private readonly Sampler sampler;
    private readonly GraphicsPipeline toneMappingPipeline;
    private readonly GraphicsPipeline antialiasingPipeline;

    private Texture displayColor = null!;

    public OutputPass(uint width, uint height)
    {
        constants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = 512,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        AttachmentFormats formats = new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            SampleCount = SampleCount.Count1
        };
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang"), "FullscreenVS"));
        using Shader toneMappingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang"), "ToneMappingFS"));

        toneMappingPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = toneMappingFragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = formats,
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        using Shader antialiasingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang"), "AntialiasingFS"));

        antialiasingPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = antialiasingFragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = formats,
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        CreateTextures(width, height);
    }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture color)
    {
        OutputConstants toneMappingConstants = new()
        {
            TexelSize = new(1.0f / Color.Desc.Width, 1.0f / Color.Desc.Height),
            Exposure = 1.0f,
            Input = color.SampledHandle,
            Sampler = sampler.Handle
        };

        constants.Upload(0, new()
        {
            Pointer = (nint)(&toneMappingConstants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        Draw(commandBuffer, toneMappingPipeline, displayColor, 0);

        OutputConstants antialiasingConstants = toneMappingConstants with { Input = displayColor.SampledHandle };

        constants.Upload(256, new()
        {
            Pointer = (nint)(&antialiasingConstants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        Draw(commandBuffer, antialiasingPipeline, Color, 256);
    }

    public void Resize(uint width, uint height)
    {
        DestroyTextures();
        CreateTextures(width, height);
    }

    protected override void Destroy()
    {
        DestroyTextures();
        antialiasingPipeline?.Dispose();
        toneMappingPipeline?.Dispose();
        sampler?.Dispose();
        constants?.Dispose();
    }

    private void CreateTextures(uint width, uint height)
    {
        TextureDesc desc = TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.Sampled
        };
        Color = App.Context.CreateTexture(desc);
        displayColor = App.Context.CreateTexture(desc);
    }

    private void DestroyTextures()
    {
        displayColor?.Dispose();
        displayColor = null!;
        Color?.Dispose();
        Color = null!;
    }

    private void Draw(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Texture output, uint offsetInBytes)
    {
        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(output)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, offsetInBytes);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(output, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct OutputConstants
{
    [FieldOffset(0)]
    public Vector2 TexelSize;

    [FieldOffset(8)]
    public float Exposure;

    [FieldOffset(16)]
    public ResourceHandle Input;

    [FieldOffset(24)]
    public ResourceHandle Sampler;
}
