using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class PathTracingPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly ComputePipeline computePipeline;
    private readonly Texture[] outputs = new Texture[8];

    public PathTracingPass(uint width, uint height)
    {
        Width = width;
        Height = height;

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(PathTracingConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "PathTracing.slang"), "PathTracingCS"));

        computePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

        CreateTextures();
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture Color { get; private set; } = null!;

    public Texture Depth { get; private set; } = null!;

    public Texture Normal { get; private set; } = null!;

    public Texture Roughness { get; private set; } = null!;

    public Texture MotionVectors { get; private set; } = null!;

    public Texture DiffuseAlbedo { get; private set; } = null!;

    public Texture SpecularAlbedo { get; private set; } = null!;

    public Texture SpecularHitDistance { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, CameraHandler camera, Matrix4x4 previousViewProjection, Vector2 jitter, uint frameIndex, bool useRayReconstruction)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        PathTracingConstants constants = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = view * projection,
            PreviousViewProjection = previousViewProjection,
            PositionFrame = new(camera.Position, BitConverter.UInt32BitsToSingle(frameIndex)),
            RenderSizeJitter = new(Width, Height, jitter.X, jitter.Y),
            HitDistance = useRayReconstruction ? 1u : 0u,
            Scene = scene.AccelerationStructure,
            Vertices = scene.Vertices,
            Indices = scene.Indices,
            Materials = scene.Materials,
            Instances = scene.Instances,
            Color = Color.StorageHandle,
            Depth = Depth.StorageHandle,
            Normal = Normal.StorageHandle,
            Roughness = Roughness.StorageHandle,
            MotionVectors = MotionVectors.StorageHandle,
            DiffuseAlbedo = DiffuseAlbedo.StorageHandle,
            SpecularAlbedo = SpecularAlbedo.StorageHandle,
            SpecularHitDistance = SpecularHitDistance.StorageHandle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(PathTracingConstants)
        });

        foreach (Texture output in outputs)
        {
            commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        ThreadGroupSize group = computePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        foreach (Texture output in outputs)
        {
            commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);
        }
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height)
        {
            return;
        }

        DestroyTextures();

        Width = width;
        Height = height;

        CreateTextures();
    }

    protected override void Destroy()
    {
        DestroyTextures();
        computePipeline?.Dispose();
        buffer?.Dispose();
    }

    private void CreateTextures()
    {
        Color = outputs[0] = CreateTexture(PixelFormat.R16G16B16A16Float);
        Depth = outputs[1] = CreateTexture(PixelFormat.R32Float);
        Normal = outputs[2] = CreateTexture(PixelFormat.R16G16B16A16Float);
        Roughness = outputs[3] = CreateTexture(PixelFormat.R16Float);
        MotionVectors = outputs[4] = CreateTexture(PixelFormat.R16G16Float);
        DiffuseAlbedo = outputs[5] = CreateTexture(PixelFormat.R16G16B16A16Float);
        SpecularAlbedo = outputs[6] = CreateTexture(PixelFormat.R16G16B16A16Float);
        SpecularHitDistance = outputs[7] = CreateTexture(PixelFormat.R32Float);
    }

    private void DestroyTextures()
    {
        for (int i = outputs.Length - 1; i >= 0; i--)
        {
            outputs[i]?.Dispose();
            outputs[i] = null!;
        }
    }

    private Texture CreateTexture(PixelFormat format)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(format, Width, Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 416)]
file struct PathTracingConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InverseView;

    [FieldOffset(64)]
    public Matrix4x4 InverseProjection;

    [FieldOffset(128)]
    public Matrix4x4 ViewProjection;

    [FieldOffset(192)]
    public Matrix4x4 PreviousViewProjection;

    [FieldOffset(256)]
    public Vector4 PositionFrame;

    [FieldOffset(272)]
    public Vector4 RenderSizeJitter;

    [FieldOffset(288)]
    public uint HitDistance;

    [FieldOffset(304)]
    public ResourceHandle Scene;

    [FieldOffset(312)]
    public ResourceHandle Vertices;

    [FieldOffset(320)]
    public ResourceHandle Indices;

    [FieldOffset(328)]
    public ResourceHandle Materials;

    [FieldOffset(336)]
    public ResourceHandle Instances;

    [FieldOffset(344)]
    public ResourceHandle Color;

    [FieldOffset(352)]
    public ResourceHandle Depth;

    [FieldOffset(360)]
    public ResourceHandle Normal;

    [FieldOffset(368)]
    public ResourceHandle Roughness;

    [FieldOffset(376)]
    public ResourceHandle MotionVectors;

    [FieldOffset(384)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(392)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(400)]
    public ResourceHandle SpecularHitDistance;
}
