using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private const int BloomLevels = 5;
    private const int ConstantSlotCount = (BloomLevels * 2) + 1;
    private const uint FlagFirstMip = 1u << 0;
    private const uint FlagOutputLuminance = 1u << 1;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline bloomDownsamplePipeline;
    private readonly ComputePipeline bloomUpsamplePipeline;
    private readonly GraphicsPipeline toneMappingPipeline;
    private readonly GraphicsPipeline antialiasingPipeline;
    private readonly Sampler linearSampler;
    private readonly Texture[] downsampleMips = new Texture[BloomLevels];
    private readonly Texture[] upsampleMips = new Texture[BloomLevels - 1];

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
        uint stride = (uint)sizeof(OutputConstants);
        OutputConstants* slots = stackalloc OutputConstants[ConstantSlotCount];

        float invExposure = 1.0f / MathF.Max(frame.Environment.Exposure, 0.1f);
        float threshold = 1.15f * invExposure;
        float knee = 0.65f * invExposure;

        Texture prevSource = input;
        for (int i = 0; i < BloomLevels; i++)
        {
            Texture dst = downsampleMips[i];
            slots[i] = new()
            {
                Width = dst.Desc.Width,
                Height = dst.Desc.Height,
                FrameIndex = frame.FrameIndex,
                Flags = i is 0 ? FlagFirstMip : 0u,
                InvSourceSize = new(1.0f / Math.Max(prevSource.Desc.Width, 1u), 1.0f / Math.Max(prevSource.Desc.Height, 1u)),
                ParamA = threshold,
                ParamB = knee,
                PrimaryTexture = prevSource.SampledHandle,
                SecondaryTexture = default,
                Destination = dst.StorageHandle,
                LinearSampler = linearSampler.Handle
            };
            prevSource = dst;
        }

        Texture upSource = downsampleMips[BloomLevels - 1];
        for (int i = BloomLevels - 2; i >= 0; i--)
        {
            Texture detail = downsampleMips[i];
            Texture dst = upsampleMips[i];
            int slotIndex = BloomLevels + (BloomLevels - 2 - i);
            slots[slotIndex] = new()
            {
                Width = dst.Desc.Width,
                Height = dst.Desc.Height,
                FrameIndex = frame.FrameIndex,
                Flags = 0u,
                InvSourceSize = new(1.0f / Math.Max(upSource.Desc.Width, 1u), 1.0f / Math.Max(upSource.Desc.Height, 1u)),
                ParamA = 1.15f,
                ParamB = 0.0f,
                PrimaryTexture = upSource.SampledHandle,
                SecondaryTexture = detail.SampledHandle,
                Destination = dst.StorageHandle,
                LinearSampler = linearSampler.Handle
            };
            upSource = dst;
        }

        int toneMappingSlot = (BloomLevels * 2) - 1;
        Texture toneMapTarget = toneMappedColor ?? Color;
        slots[toneMappingSlot] = new()
        {
            Width = toneMapTarget.Desc.Width,
            Height = toneMapTarget.Desc.Height,
            FrameIndex = frame.FrameIndex,
            Flags = toneMappedColor is not null ? FlagOutputLuminance : 0u,
            InvSourceSize = Vector2.Zero,
            ParamA = frame.Environment.Exposure,
            ParamB = 0.045f,
            PrimaryTexture = input.SampledHandle,
            SecondaryTexture = upsampleMips[0].SampledHandle,
            Destination = default,
            LinearSampler = linearSampler.Handle
        };

        int activeSlots = ConstantSlotCount - 1;
        if (toneMappedColor is not null)
        {
            slots[ConstantSlotCount - 1] = new()
            {
                Width = Color.Desc.Width,
                Height = Color.Desc.Height,
                FrameIndex = frame.FrameIndex,
                Flags = 0u,
                InvSourceSize = new(1.0f / Math.Max(toneMappedColor.Desc.Width, 1u), 1.0f / Math.Max(toneMappedColor.Desc.Height, 1u)),
                ParamA = 0.0f,
                ParamB = 0.0f,
                PrimaryTexture = toneMappedColor.SampledHandle,
                SecondaryTexture = default,
                Destination = default,
                LinearSampler = linearSampler.Handle
            };
            activeSlots = ConstantSlotCount;
        }

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)slots,
            SizeInBytes = stride * (uint)activeSlots
        });

        ThreadGroupSize downGroup = bloomDownsamplePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;
        commandBuffer.SetPipeline(bloomDownsamplePipeline);

        for (int i = 0; i < BloomLevels; i++)
        {
            Texture dst = downsampleMips[i];
            commandBuffer.Transition(dst, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.SetConstantBuffer(constantBuffer, (uint)(i * stride));
            commandBuffer.Dispatch((dst.Desc.Width + downGroup.X - 1) / downGroup.X, (dst.Desc.Height + downGroup.Y - 1) / downGroup.Y, 1);
            commandBuffer.Transition(dst, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        }

        ThreadGroupSize upGroup = bloomUpsamplePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;
        commandBuffer.SetPipeline(bloomUpsamplePipeline);

        for (int i = BloomLevels - 2; i >= 0; i--)
        {
            Texture dst = upsampleMips[i];
            uint slotIndex = (uint)(BloomLevels + (BloomLevels - 2 - i));
            commandBuffer.Transition(dst, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.SetConstantBuffer(constantBuffer, slotIndex * stride);
            commandBuffer.Dispatch((dst.Desc.Width + upGroup.X - 1) / upGroup.X, (dst.Desc.Height + upGroup.Y - 1) / upGroup.Y, 1);
            commandBuffer.Transition(dst, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Barrier(BarrierStages.ComputeShading, i is 0 ? BarrierStages.FragmentShading : BarrierStages.ComputeShading);
        }

        DrawFullscreen(commandBuffer, toneMappingPipeline, toneMapTarget, (uint)(toneMappingSlot * stride));

        if (toneMappedColor is not null)
        {
            DrawFullscreen(commandBuffer, antialiasingPipeline, Color, (uint)((ConstantSlotCount - 1) * stride));
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
        for (int i = 0; i < BloomLevels; i++)
        {
            mipWidth = Math.Max(1u, mipWidth / 2u);
            mipHeight = Math.Max(1u, mipHeight / 2u);

            downsampleMips[i] = CreateBloomTexture(mipWidth, mipHeight);

            if (i < BloomLevels - 1)
            {
                upsampleMips[i] = CreateBloomTexture(mipWidth, mipHeight);
            }
        }
    }

    private void DestroyResources()
    {
        for (int i = 0; i < upsampleMips.Length; i++)
        {
            upsampleMips[i]?.Dispose();
            upsampleMips[i] = null!;
        }

        for (int i = 0; i < downsampleMips.Length; i++)
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

    private void DrawFullscreen(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Texture target, uint constantOffset)
    {
        commandBuffer.Transition(target, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(target)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, constantOffset);
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
    public Vector2 InvSourceSize;

    [FieldOffset(24)]
    public float ParamA;

    [FieldOffset(28)]
    public float ParamB;

    [FieldOffset(32)]
    public ResourceHandle PrimaryTexture;

    [FieldOffset(40)]
    public ResourceHandle SecondaryTexture;

    [FieldOffset(48)]
    public ResourceHandle Destination;

    [FieldOffset(56)]
    public ResourceHandle LinearSampler;
}
