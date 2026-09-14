using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe partial class DenoisePass : DisposableObject
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer buffer;
    private readonly ComputePipeline temporalPipeline;
    private readonly ComputePipeline atrousPipeline;

    private Texture[] colorHistory = [];
    private Texture[] momentsHistory = [];
    private Texture[] geometryHistory = [];
    private Texture[] filtered = [];
    private Texture[] resolveHistory = [];

    private bool resourcesInitialized;
    private int historyIndex;

    public DenoisePass()
    {
        using Shader temporalShader = App.Context.CreateShader(App.Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => DirectX12TemporalMain,
            GraphicsApi.Metal => MetalTemporalMain,
            GraphicsApi.Vulkan => VulkanTemporalMain,
            _ => default
        });
        using Shader atrousShader = App.Context.CreateShader(App.Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => DirectX12AtrousMain,
            GraphicsApi.Metal => MetalAtrousMain,
            GraphicsApi.Vulkan => VulkanAtrousMain,
            _ => default
        });

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = 6 * 256,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        temporalPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = temporalShader });
        atrousPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = atrousShader });
    }

    public Texture Color => filtered[1];

    public Texture ResolvedColor => resolveHistory[1 - historyIndex];

    public void Render(CommandBuffer commandBuffer, Texture color, Texture normal, Texture depth, Vector2 jitter, Vector2 previousJitter, bool sameCamera, Matrix4x4 clipToPrevClip)
    {
        if (!resourcesInitialized)
        {
            for (int index = 0; index < 2; index++)
            {
                commandBuffer.Transition(colorHistory[index], default, TextureLayout.Undefined, TextureLayout.Sampled);
                commandBuffer.Transition(momentsHistory[index], default, TextureLayout.Undefined, TextureLayout.Sampled);
                commandBuffer.Transition(geometryHistory[index], default, TextureLayout.Undefined, TextureLayout.Sampled);
                commandBuffer.Transition(filtered[index], default, TextureLayout.Undefined, TextureLayout.Sampled);
                commandBuffer.Transition(resolveHistory[index], default, TextureLayout.Undefined, TextureLayout.Sampled);
            }
        }

        Temporal(commandBuffer, color, normal, depth, jitter, previousJitter, sameCamera, clipToPrevClip);

        for (uint iteration = 0; iteration < 5; iteration++)
        {
            Atrous(commandBuffer, normal, depth, jitter, previousJitter, sameCamera, clipToPrevClip, iteration);
        }

        historyIndex = 1 - historyIndex;
        resourcesInitialized = true;
    }

    public void Resize(uint width, uint height)
    {
        for (int index = filtered.Length - 1; index >= 0; index--)
        {
            resolveHistory[index].Dispose();
            filtered[index].Dispose();
            geometryHistory[index].Dispose();
            momentsHistory[index].Dispose();
            colorHistory[index].Dispose();
        }

        colorHistory = new Texture[2];
        momentsHistory = new Texture[2];
        geometryHistory = new Texture[2];
        filtered = new Texture[2];
        resolveHistory = new Texture[2];

        TextureDesc desc = new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        };

        for (int index = 0; index < 2; index++)
        {
            colorHistory[index] = App.Context.CreateTexture(desc);
            momentsHistory[index] = App.Context.CreateTexture(desc with { Format = PixelFormat.R32G32B32A32Float });
            geometryHistory[index] = App.Context.CreateTexture(desc);
            filtered[index] = App.Context.CreateTexture(desc);
            resolveHistory[index] = App.Context.CreateTexture(desc with { Format = PixelFormat.R32G32B32A32Float });
        }

        resourcesInitialized = false;
        historyIndex = 0;
    }

    protected override void Destroy()
    {
        for (int index = filtered.Length - 1; index >= 0; index--)
        {
            resolveHistory[index].Dispose();
            filtered[index].Dispose();
            geometryHistory[index].Dispose();
            momentsHistory[index].Dispose();
            colorHistory[index].Dispose();
        }

        atrousPipeline.Dispose();
        temporalPipeline.Dispose();
        buffer.Dispose();
    }

    private void Temporal(CommandBuffer commandBuffer, Texture color, Texture normal, Texture depth, Vector2 jitter, Vector2 previousJitter, bool sameCamera, Matrix4x4 clipToPrevClip)
    {
        int previousIndex = 1 - historyIndex;
        TemporalConstants constants = new()
        {
            SizeRcp = new(color.Desc.Width, color.Desc.Height, 1.0f / color.Desc.Width, 1.0f / color.Desc.Height),
            Jitter = new(jitter, previousJitter.X, previousJitter.Y),
            Reset = resourcesInitialized ? 0u : 1u,
            SameCamera = sameCamera ? 1u : 0u,
            Color = color.SampledHandle,
            Normal = normal.SampledHandle,
            Depth = depth.SampledHandle,
            PreviousColor = colorHistory[previousIndex].SampledHandle,
            PreviousMoments = momentsHistory[previousIndex].SampledHandle,
            PreviousGeometry = geometryHistory[previousIndex].SampledHandle,
            MomentsHistory = momentsHistory[historyIndex].StorageHandle,
            GeometryHistory = geometryHistory[historyIndex].StorageHandle,
            Output = filtered[0].StorageHandle,
            ClipToPrevClip = clipToPrevClip
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(TemporalConstants)
        });

        commandBuffer.Transition(momentsHistory[historyIndex], default, TextureLayout.Sampled, TextureLayout.Storage);
        commandBuffer.Transition(geometryHistory[historyIndex], default, TextureLayout.Sampled, TextureLayout.Storage);
        commandBuffer.Transition(filtered[0], default, TextureLayout.Sampled, TextureLayout.Storage);
        commandBuffer.SetPipeline(temporalPipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((color.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize, (color.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(momentsHistory[historyIndex], default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(geometryHistory[historyIndex], default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(filtered[0], default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    private void Atrous(CommandBuffer commandBuffer, Texture normal, Texture depth, Vector2 jitter, Vector2 previousJitter, bool sameCamera, Matrix4x4 clipToPrevClip, uint iteration)
    {
        Texture input = filtered[iteration % 2];
        Texture output = filtered[1 - (iteration % 2)];
        AtrousConstants constants = new()
        {
            SizeRcp = new(input.Desc.Width, input.Desc.Height, 1.0f / input.Desc.Width, 1.0f / input.Desc.Height),
            Step = 1u << (int)iteration,
            Reset = resourcesInitialized ? 0u : 1u,
            SameCamera = sameCamera ? 1u : 0u,
            Normal = normal.SampledHandle,
            Input = input.SampledHandle,
            Output = output.StorageHandle,
            ColorHistory = colorHistory[historyIndex].StorageHandle,
            Depth = depth.SampledHandle,
            PreviousGeometry = geometryHistory[1 - historyIndex].SampledHandle,
            PreviousResolved = resolveHistory[1 - historyIndex].SampledHandle,
            ResolvedHistory = resolveHistory[historyIndex].StorageHandle,
            Jitter = new(jitter, previousJitter.X, previousJitter.Y),
            ClipToPrevClip = clipToPrevClip
        };
        uint offset = (iteration + 1) * 256;

        buffer.Upload(offset, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(AtrousConstants)
        });

        if (iteration is 0)
        {
            commandBuffer.Transition(colorHistory[historyIndex], default, TextureLayout.Sampled, TextureLayout.Storage);
        }

        commandBuffer.Transition(output, default, TextureLayout.Sampled, TextureLayout.Storage);

        if (iteration is 4)
        {
            commandBuffer.Transition(resolveHistory[historyIndex], default, TextureLayout.Sampled, TextureLayout.Storage);
        }

        commandBuffer.SetPipeline(atrousPipeline);
        commandBuffer.SetConstantBuffer(buffer, offset);
        commandBuffer.Dispatch((input.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize, (input.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (iteration is 4)
        {
            commandBuffer.Transition(colorHistory[historyIndex], default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(resolveHistory[historyIndex], default, TextureLayout.Storage, TextureLayout.Sampled);
        }
    }
}

[StructLayout(LayoutKind.Explicit, Size = 192)]
file struct TemporalConstants
{
    [FieldOffset(0)]
    public Vector4 SizeRcp;

    [FieldOffset(16)]
    public Vector4 Jitter;

    [FieldOffset(32)]
    public uint Reset;

    [FieldOffset(36)]
    public uint SameCamera;

    [FieldOffset(40)]
    public uint ResetPaddingZ;

    [FieldOffset(44)]
    public uint ResetPaddingW;

    [FieldOffset(48)]
    public ResourceHandle Color;

    [FieldOffset(56)]
    public ResourceHandle Normal;

    [FieldOffset(64)]
    public ResourceHandle Depth;

    [FieldOffset(72)]
    public ResourceHandle PreviousColor;

    [FieldOffset(80)]
    public ResourceHandle PreviousMoments;

    [FieldOffset(88)]
    public ResourceHandle PreviousGeometry;

    [FieldOffset(96)]
    public ResourceHandle MomentsHistory;

    [FieldOffset(104)]
    public ResourceHandle GeometryHistory;

    [FieldOffset(112)]
    public ResourceHandle Output;

    [FieldOffset(128)]
    public Matrix4x4 ClipToPrevClip;
}

[StructLayout(LayoutKind.Explicit, Size = 176)]
file struct AtrousConstants
{
    [FieldOffset(0)]
    public Vector4 SizeRcp;

    [FieldOffset(16)]
    public uint Step;

    [FieldOffset(20)]
    public uint Reset;

    [FieldOffset(24)]
    public uint SameCamera;

    [FieldOffset(28)]
    public uint StepPaddingW;

    [FieldOffset(32)]
    public ResourceHandle Normal;

    [FieldOffset(40)]
    public ResourceHandle Input;

    [FieldOffset(48)]
    public ResourceHandle Output;

    [FieldOffset(56)]
    public ResourceHandle ColorHistory;

    [FieldOffset(64)]
    public ResourceHandle Depth;

    [FieldOffset(72)]
    public ResourceHandle PreviousGeometry;

    [FieldOffset(80)]
    public ResourceHandle PreviousResolved;

    [FieldOffset(88)]
    public ResourceHandle ResolvedHistory;

    [FieldOffset(96)]
    public Vector4 Jitter;

    [FieldOffset(112)]
    public Matrix4x4 ClipToPrevClip;
}
