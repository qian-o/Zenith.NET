using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private const int BloomLevelCount = 5;
    private const int ConstantSlotCount = (BloomLevelCount * 2) + 1;
    private const uint FlagFirstMip = 1 << 0;
    private const uint FlagOutputLuminance = 1 << 1;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline bloomDownsamplePipeline;
    private readonly ComputePipeline bloomUpsamplePipeline;
    private readonly GraphicsPipeline toneMappingPipeline;
    private readonly GraphicsPipeline antialiasingPipeline;
    private readonly Sampler linearSampler;
    private readonly Texture[] downsampleMips = new Texture[BloomLevelCount];
    private readonly Texture[] upsampleMips = new Texture[BloomLevelCount - 1];

    private Texture? toneMappedColor;

    public OutputPass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(OutputConstants) * ConstantSlotCount),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang");

        using Shader downsampleShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "BloomDownsampleCS"));
        using Shader upsampleShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "BloomUpsampleCS"));
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FullscreenVS"));
        using Shader toneMappingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ToneMappingFS"));
        using Shader antialiasingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "AntialiasingFS"));

        bloomDownsamplePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = downsampleShader });
        bloomUpsamplePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = upsampleShader });
        toneMappingPipeline = CreateFullscreenPipeline(vertexShader, toneMappingFragmentShader);
        antialiasingPipeline = CreateFullscreenPipeline(vertexShader, antialiasingFragmentShader);

        linearSampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture input, in FrameContext frame)
    {
        uint strideInBytes = (uint)sizeof(OutputConstants);
        OutputConstants* slots = stackalloc OutputConstants[ConstantSlotCount];

        float inverseExposure = 1.0f / MathF.Max(frame.Environment.Exposure, 0.1f);
        float threshold = 1.15f * inverseExposure;
        float knee = 0.65f * inverseExposure;

        Texture src = input;
        for (int i = 0; i < BloomLevelCount; i++)
        {
            Texture dst = downsampleMips[i];
            slots[i] = new()
            {
                Width = dst.Desc.Width,
                Height = dst.Desc.Height,
                FrameIndex = frame.FrameIndex,
                Flags = i is 0 ? FlagFirstMip : 0u,
                TexelSize = new(1.0f / Math.Max(src.Desc.Width, 1u), 1.0f / Math.Max(src.Desc.Height, 1u)),
                ParameterA = threshold,
                ParameterB = knee,
                PrimaryTexture = src.SampledHandle,
                SecondaryTexture = default,
                Destination = dst.StorageHandle,
                LinearSampler = linearSampler.Handle
            };
            src = dst;
        }

        Texture upsampleSrc = downsampleMips[BloomLevelCount - 1];
        for (int i = BloomLevelCount - 2; i >= 0; i--)
        {
            Texture detail = downsampleMips[i];
            Texture dst = upsampleMips[i];
            int slotIndex = BloomLevelCount + (BloomLevelCount - 2 - i);
            slots[slotIndex] = new()
            {
                Width = dst.Desc.Width,
                Height = dst.Desc.Height,
                FrameIndex = frame.FrameIndex,
                Flags = 0u,
                TexelSize = new(1.0f / Math.Max(upsampleSrc.Desc.Width, 1u), 1.0f / Math.Max(upsampleSrc.Desc.Height, 1u)),
                ParameterA = 1.15f,
                ParameterB = 0.0f,
                PrimaryTexture = upsampleSrc.SampledHandle,
                SecondaryTexture = detail.SampledHandle,
                Destination = dst.StorageHandle,
                LinearSampler = linearSampler.Handle
            };
            upsampleSrc = dst;
        }

        int toneMappingSlot = (BloomLevelCount * 2) - 1;
        Texture toneMappingTarget = toneMappedColor ?? Color;
        slots[toneMappingSlot] = new()
        {
            Width = toneMappingTarget.Desc.Width,
            Height = toneMappingTarget.Desc.Height,
            FrameIndex = frame.FrameIndex,
            Flags = toneMappedColor is not null ? FlagOutputLuminance : 0u,
            TexelSize = Vector2.Zero,
            ParameterA = frame.Environment.Exposure,
            ParameterB = 0.045f,
            PrimaryTexture = input.SampledHandle,
            SecondaryTexture = upsampleMips[0].SampledHandle,
            Destination = default,
            LinearSampler = linearSampler.Handle
        };

        int activeSlotCount = ConstantSlotCount - 1;

        if (toneMappedColor is not null)
        {
            slots[ConstantSlotCount - 1] = new()
            {
                Width = Color.Desc.Width,
                Height = Color.Desc.Height,
                FrameIndex = frame.FrameIndex,
                Flags = 0u,
                TexelSize = new(1.0f / Math.Max(toneMappedColor.Desc.Width, 1u), 1.0f / Math.Max(toneMappedColor.Desc.Height, 1u)),
                ParameterA = 0.0f,
                ParameterB = 0.0f,
                PrimaryTexture = toneMappedColor.SampledHandle,
                SecondaryTexture = default,
                Destination = default,
                LinearSampler = linearSampler.Handle
            };
            activeSlotCount = ConstantSlotCount;
        }

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)slots,
            SizeInBytes = strideInBytes * (uint)activeSlotCount
        });

        ThreadGroupSize downsampleGroup = bloomDownsamplePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;
        commandBuffer.SetPipeline(bloomDownsamplePipeline);

        for (int i = 0; i < BloomLevelCount; i++)
        {
            Texture dst = downsampleMips[i];
            commandBuffer.Transition(dst, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.SetConstantBuffer(constantBuffer, (uint)(i * strideInBytes));
            commandBuffer.Dispatch((dst.Desc.Width + downsampleGroup.X - 1) / downsampleGroup.X, (dst.Desc.Height + downsampleGroup.Y - 1) / downsampleGroup.Y, 1);
            commandBuffer.Transition(dst, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        }

        ThreadGroupSize upsampleGroup = bloomUpsamplePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;
        commandBuffer.SetPipeline(bloomUpsamplePipeline);

        for (int i = BloomLevelCount - 2; i >= 0; i--)
        {
            Texture dst = upsampleMips[i];
            uint slotIndex = (uint)(BloomLevelCount + (BloomLevelCount - 2 - i));
            commandBuffer.Transition(dst, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.SetConstantBuffer(constantBuffer, slotIndex * strideInBytes);
            commandBuffer.Dispatch((dst.Desc.Width + upsampleGroup.X - 1) / upsampleGroup.X, (dst.Desc.Height + upsampleGroup.Y - 1) / upsampleGroup.Y, 1);
            commandBuffer.Transition(dst, default, TextureLayout.Storage, TextureLayout.Sampled);

            if (i > 0)
            {
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            }
        }

        DrawFullscreen(commandBuffer, toneMappingPipeline, toneMappingTarget, (uint)(toneMappingSlot * strideInBytes));

        if (toneMappedColor is not null)
        {
            DrawFullscreen(commandBuffer, antialiasingPipeline, Color, (uint)((ConstantSlotCount - 1) * strideInBytes));
        }
    }

    public void Resize(uint width, uint height, bool hasAntialiasing)
    {
        if (Width == width && Height == height && (toneMappedColor is not null) == hasAntialiasing)
        {
            return;
        }

        DestroyResources();
        CreateResources(width, height, hasAntialiasing);
    }

    protected override void Destroy()
    {
        DestroyResources();

        linearSampler?.Dispose();
        antialiasingPipeline?.Dispose();
        toneMappingPipeline?.Dispose();
        bloomUpsamplePipeline?.Dispose();
        bloomDownsamplePipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void CreateResources(uint width, uint height, bool hasAntialiasing)
    {
        Width = width;
        Height = height;

        TextureDesc colorDesc = TextureDesc.ColorAttachment(App.ColorFormat, width, height, 1, SampleCount.Count1);

        Color = App.Context.CreateTexture(colorDesc with
        {
            Usages = colorDesc.Usages | TextureUsages.UpscalingInput
        });

        if (hasAntialiasing)
        {
            toneMappedColor = App.Context.CreateTexture(colorDesc);
        }

        uint mipWidth = width;
        uint mipHeight = height;
        for (int i = 0; i < BloomLevelCount; i++)
        {
            mipWidth = Math.Max(1u, mipWidth / 2u);
            mipHeight = Math.Max(1u, mipHeight / 2u);

            downsampleMips[i] = CreateBloomTexture(mipWidth, mipHeight);

            if (i < BloomLevelCount - 1)
            {
                upsampleMips[i] = CreateBloomTexture(mipWidth, mipHeight);
            }
        }
    }

    private void DestroyResources()
    {
        for (int i = upsampleMips.Length - 1; i >= 0; i--)
        {
            upsampleMips[i]?.Dispose();
            upsampleMips[i] = null!;
        }

        for (int i = downsampleMips.Length - 1; i >= 0; i--)
        {
            downsampleMips[i]?.Dispose();
            downsampleMips[i] = null!;
        }

        toneMappedColor?.Dispose();
        toneMappedColor = null;
        Color?.Dispose();
        Color = null!;

        Width = 0;
        Height = 0;
    }

    private void DrawFullscreen(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Texture target, uint offsetInBytes)
    {
        commandBuffer.Transition(target, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(target)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, offsetInBytes);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(target, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    private static GraphicsPipeline CreateFullscreenPipeline(Shader vertexShader, Shader fragmentShader)
    {
        return App.Context.CreateGraphicsPipeline(new()
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
    }

    private static Texture CreateBloomTexture(uint width, uint height)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct OutputConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public uint FrameIndex;

    [FieldOffset(12)]
    public uint Flags;

    [FieldOffset(16)]
    public Vector2 TexelSize;

    [FieldOffset(24)]
    public float ParameterA;

    [FieldOffset(28)]
    public float ParameterB;

    [FieldOffset(32)]
    public ResourceHandle PrimaryTexture;

    [FieldOffset(40)]
    public ResourceHandle SecondaryTexture;

    [FieldOffset(48)]
    public ResourceHandle Destination;

    [FieldOffset(56)]
    public ResourceHandle LinearSampler;
}
