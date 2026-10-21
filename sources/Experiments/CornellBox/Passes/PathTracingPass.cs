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

    public void Record(CommandBuffer commandBuffer, SceneResources scene, CameraHandler camera, uint frameIndex)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

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
            Color = Color.StorageHandle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(PathTracingConstants)
        });

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        ThreadGroupSize group = computePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);
        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
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
    }

    protected override void Destroy()
    {
        DestroyTexture();

        computePipeline?.Dispose();
        buffer?.Dispose();
    }

    private void CreateTexture()
    {
        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, Width, Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }

    private void DestroyTexture()
    {
        Color?.Dispose();
        Color = null!;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 208)]
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
}
