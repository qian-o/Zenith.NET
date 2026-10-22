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

    private Texture[] textures = [];
    private Matrix4x4 lastViewProjection;

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

        CreateTexture();
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture Color { get; private set; } = null!;

    public Texture Depth { get; private set; } = null!;

    public Texture MotionVectors { get; private set; } = null!;

    public Texture DiffuseAlbedo { get; private set; } = null!;

    public Texture SpecularAlbedo { get; private set; } = null!;

    public Texture Normals { get; private set; } = null!;

    public Texture Roughness { get; private set; } = null!;

    public Texture SpecularHitDistance { get; private set; } = null!;

    public Vector2 JitterOffset { get; set; }

    public bool ResetHistory { get; set; } = true;

    public Matrix4x4 View { get; private set; }

    public Matrix4x4 Projection { get; private set; }

    public Matrix4x4 ViewProjection { get; private set; }

    public Matrix4x4 PreviousViewProjection { get; private set; }

    public void Record(CommandBuffer commandBuffer, SceneResources scene, CameraHandler camera, uint frameIndex)
    {
        View = camera.View;
        Projection = camera.Projection;
        ViewProjection = View * Projection;
        PreviousViewProjection = ResetHistory ? ViewProjection : lastViewProjection;
        Matrix4x4.Invert(View, out Matrix4x4 inverseView);
        Matrix4x4.Invert(Projection, out Matrix4x4 inverseProjection);

        PathTracingConstants constants = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            PositionFrame = new(camera.Position, BitConverter.UInt32BitsToSingle(frameIndex)),
            RenderSize = new(Width, Height),
            Scene = scene.AccelerationStructure,
            Vertices = scene.Vertices,
            Indices = scene.Indices,
            Materials = scene.Materials,
            Instances = scene.Instances,
            Color = Color.StorageHandle,
            ViewProjection = ViewProjection,
            PreviousViewProjection = PreviousViewProjection,
            JitterOffset = JitterOffset,
            Depth = Depth.StorageHandle,
            MotionVectors = MotionVectors.StorageHandle,
            DiffuseAlbedo = DiffuseAlbedo.StorageHandle,
            SpecularAlbedo = SpecularAlbedo.StorageHandle,
            Normals = Normals.StorageHandle,
            Roughness = Roughness.StorageHandle,
            SpecularHitDistance = SpecularHitDistance.StorageHandle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(PathTracingConstants)
        });

        foreach (Texture texture in textures)
        {
            commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        ThreadGroupSize group = computePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);

        foreach (Texture texture in textures)
        {
            commandBuffer.Transition(texture, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        lastViewProjection = ViewProjection;
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height)
        {
            return;
        }

        DestroyTexture();

        Width = width;
        Height = height;

        CreateTexture();
        ResetHistory = true;
    }

    protected override void Destroy()
    {
        DestroyTexture();

        computePipeline?.Dispose();
        buffer?.Dispose();
    }

    private void CreateTexture()
    {
        Color = CreateTexture(PixelFormat.R16G16B16A16Float);
        Depth = CreateTexture(PixelFormat.R32Float);
        MotionVectors = CreateTexture(PixelFormat.R32G32Float);
        DiffuseAlbedo = CreateTexture(PixelFormat.R16G16B16A16Float);
        SpecularAlbedo = CreateTexture(PixelFormat.R16G16B16A16Float);
        Normals = CreateTexture(PixelFormat.R16G16B16A16Float);
        Roughness = CreateTexture(PixelFormat.R16Float);
        SpecularHitDistance = CreateTexture(PixelFormat.R32Float);

        textures = [Color, Depth, MotionVectors, DiffuseAlbedo, SpecularAlbedo, Normals, Roughness, SpecularHitDistance];
    }

    private Texture CreateTexture(PixelFormat format)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(format, Width, Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }

    private void DestroyTexture()
    {
        SpecularHitDistance?.Dispose();
        SpecularHitDistance = null!;

        Roughness?.Dispose();
        Roughness = null!;

        Normals?.Dispose();
        Normals = null!;

        SpecularAlbedo?.Dispose();
        SpecularAlbedo = null!;

        DiffuseAlbedo?.Dispose();
        DiffuseAlbedo = null!;

        MotionVectors?.Dispose();
        MotionVectors = null!;

        Depth?.Dispose();
        Depth = null!;

        Color?.Dispose();
        Color = null!;

        textures = [];
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
    public Vector4 PositionFrame;

    [FieldOffset(144)]
    public Vector2 RenderSize;

    [FieldOffset(160)]
    public ResourceHandle Scene;

    [FieldOffset(168)]
    public ResourceHandle Vertices;

    [FieldOffset(176)]
    public ResourceHandle Indices;

    [FieldOffset(184)]
    public ResourceHandle Materials;

    [FieldOffset(192)]
    public ResourceHandle Instances;

    [FieldOffset(200)]
    public ResourceHandle Color;

    [FieldOffset(208)]
    public Matrix4x4 ViewProjection;

    [FieldOffset(272)]
    public Matrix4x4 PreviousViewProjection;

    [FieldOffset(336)]
    public Vector2 JitterOffset;

    [FieldOffset(352)]
    public ResourceHandle Depth;

    [FieldOffset(360)]
    public ResourceHandle MotionVectors;

    [FieldOffset(368)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(376)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(384)]
    public ResourceHandle Normals;

    [FieldOffset(392)]
    public ResourceHandle Roughness;

    [FieldOffset(400)]
    public ResourceHandle SpecularHitDistance;
}
