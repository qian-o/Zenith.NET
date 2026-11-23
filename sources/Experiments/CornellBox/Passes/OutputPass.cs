using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private const int BloomLevels = 3;
    private const int ConstantSlotCount = 6;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline bloomDownsamplePipeline;
    private readonly ComputePipeline bloomUpsamplePipeline;
    private readonly GraphicsPipeline toneMappingPipeline;
    private readonly Sampler linearSampler;
    private readonly Texture[] downsampleMips = new Texture[BloomLevels];
    private readonly Texture[] upsampleMips = new Texture[BloomLevels - 1];

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
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ToneMappingFS"));

        bloomDownsamplePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = downsampleShader });
        bloomUpsamplePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = upsampleShader });
        toneMappingPipeline = App.Context.CreateGraphicsPipeline(new()
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

        linearSampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        CreateResources();
    }

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
            float texelScale = i is 0 ? 2.0f : 1.0f;
            slots[i] = new()
            {
                Width = dst.Desc.Width,
                Height = dst.Desc.Height,
                FrameOrFirstMip = i is 0 ? 1u : 0u,
                FilterRadius = 1.0f,
                InvSourceSize = new(texelScale / Math.Max(prevSource.Desc.Width, 1u), texelScale / Math.Max(prevSource.Desc.Height, 1u)),
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
                FrameOrFirstMip = 0u,
                FilterRadius = 1.15f,
                InvSourceSize = new(1.0f / Math.Max(upSource.Desc.Width, 1u), 1.0f / Math.Max(upSource.Desc.Height, 1u)),
                ParamA = 0.0f,
                ParamB = 0.0f,
                PrimaryTexture = upSource.SampledHandle,
                SecondaryTexture = detail.SampledHandle,
                Destination = dst.StorageHandle,
                LinearSampler = linearSampler.Handle
            };
            upSource = dst;
        }

        slots[ConstantSlotCount - 1] = new()
        {
            Width = Color.Desc.Width,
            Height = Color.Desc.Height,
            FrameOrFirstMip = frame.FrameIndex,
            FilterRadius = 0.0f,
            InvSourceSize = Vector2.Zero,
            ParamA = frame.Environment.Exposure,
            ParamB = 0.045f,
            PrimaryTexture = input.SampledHandle,
            SecondaryTexture = upsampleMips[0].SampledHandle,
            Destination = default,
            LinearSampler = linearSampler.Handle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)slots,
            SizeInBytes = stride * ConstantSlotCount
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

        uint toneMappingOffset = (uint)((ConstantSlotCount - 1) * stride);

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(toneMappingPipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, toneMappingOffset);
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

        DestroyResources();
        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();

        linearSampler?.Dispose();
        toneMappingPipeline?.Dispose();
        bloomUpsamplePipeline?.Dispose();
        bloomDownsamplePipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void CreateResources()
    {
        Color = App.Context.CreateTexture(TextureDesc.ColorAttachment(App.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.UpscalingInput
        });

        uint mipWidth = Math.Max(1u, App.PhysicalWidth / 2u);
        uint mipHeight = Math.Max(1u, App.PhysicalHeight / 2u);
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

        Color?.Dispose();
        Color = null!;
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
    public uint FrameOrFirstMip;

    [FieldOffset(12)]
    public float FilterRadius;

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
