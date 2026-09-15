using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class OutputPass(uint width, uint height) : Pass(width, height)
{
    private Buffer constants = null!;
    private Sampler sampler = null!;
    private GraphicsPipeline toneMappingPipeline = null!;
    private GraphicsPipeline antialiasingPipeline = null!;
    private Texture displayColor = null!;

    public Texture Color { get; private set; } = null!;

    protected override void Initialize()
    {
        constants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = 512,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        AttachmentFormats formats = new() { ColorFormats = [PixelFormat.B8G8R8A8UNorm], SampleCount = SampleCount.Count1 };
        using Shader toneMappingVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Output.slang"), "FullscreenVS"));
        using Shader toneMappingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Output.slang"), "ToneMappingFS"));

        toneMappingPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = toneMappingVertexShader,
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

        using Shader antialiasingVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Output.slang"), "FullscreenVS"));
        using Shader antialiasingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Output.slang"), "AntialiasingFS"));

        antialiasingPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = antialiasingVertexShader,
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

        Color = CreateTexture(Width, Height, PixelFormat.B8G8R8A8UNorm, TextureUsages.ColorAttachment | TextureUsages.Sampled);
        displayColor = CreateTexture(Width, Height, PixelFormat.B8G8R8A8UNorm, TextureUsages.ColorAttachment | TextureUsages.Sampled);
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        OutputConstants toneMappingConstants = new()
        {
            TexelSize = new(1.0f / Width, 1.0f / Height),
            Exposure = 1.0f,
            Input = args.Color.SampledHandle,
            Sampler = sampler.Handle
        };

        constants.Upload(0, new()
        {
            Pointer = (nint)(&toneMappingConstants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        Draw(commandBuffer, toneMappingPipeline, displayColor, 0);

        OutputConstants antialiasingConstants = new()
        {
            TexelSize = new(1.0f / Width, 1.0f / Height),
            Exposure = 1.0f,
            Input = displayColor.SampledHandle,
            Sampler = sampler.Handle
        };

        constants.Upload(256, new()
        {
            Pointer = (nint)(&antialiasingConstants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        Draw(commandBuffer, antialiasingPipeline, Color, 256);
    }

    protected override void ResizeImpl()
    {
        if (Color.Desc.Width == Width && Color.Desc.Height == Height)
        {
            return;
        }

        Color.Dispose();
        displayColor.Dispose();

        Color = CreateTexture(Width, Height, PixelFormat.B8G8R8A8UNorm, TextureUsages.ColorAttachment | TextureUsages.Sampled);
        displayColor = CreateTexture(Width, Height, PixelFormat.B8G8R8A8UNorm, TextureUsages.ColorAttachment | TextureUsages.Sampled);
    }

    protected override void Destroy()
    {
        Color?.Dispose();
        displayColor?.Dispose();
        antialiasingPipeline.Dispose();
        toneMappingPipeline.Dispose();
        sampler.Dispose();
        constants.Dispose();
    }

    private void Draw(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Texture output, uint offset)
    {
        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(output)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, offset);
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
