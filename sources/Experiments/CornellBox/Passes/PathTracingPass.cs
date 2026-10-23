using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class PathTracingPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly ComputePipeline pipeline;

    public PathTracingPass(RenderDesc desc)
    {
        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(PathTracingConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "PathTracing.slang"), "PathTracingCS"));

        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

        Resize(desc);
    }

    public PathTracingOutput Output { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, in RenderArgs args)
    {
        Matrix4x4.Invert(args.View, out Matrix4x4 inverseView);
        Matrix4x4.Invert(args.Projection, out Matrix4x4 inverseProjection);

        PathTracingConstants constants = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = args.ViewProjection,
            PreviousViewProjection = args.PreviousViewProjection,
            Position = new(args.Camera.Position, 1.0f),
            RenderSize = new(Output.Width, Output.Height),
            JitterOffset = args.JitterOffset,
            FrameIndex = args.FrameIndex,
            Scene = args.Scene.AccelerationStructure,
            Vertices = args.Scene.Vertices,
            Indices = args.Scene.Indices,
            Materials = args.Scene.Materials,
            Instances = args.Scene.Instances,
            Color = Output.Color.StorageHandle,
            Depth = Output.Depth.StorageHandle,
            MotionVectors = Output.MotionVectors.StorageHandle,
            DiffuseAlbedo = Output.DiffuseAlbedo.StorageHandle,
            SpecularAlbedo = Output.SpecularAlbedo.StorageHandle,
            Normals = Output.Normals.StorageHandle,
            Roughness = Output.Roughness.StorageHandle,
            SpecularHitDistance = Output.SpecularHitDistance.StorageHandle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(PathTracingConstants)
        });

        foreach (Texture texture in Output.Textures)
        {
            commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        ThreadGroupSize group = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((Output.Width + group.X - 1) / group.X, (Output.Height + group.Y - 1) / group.Y, 1);

        foreach (Texture texture in Output.Textures)
        {
            commandBuffer.Transition(texture, default, TextureLayout.Storage, TextureLayout.Sampled);
        }
    }

    public void Resize(RenderDesc desc)
    {
        if (Output is not null && Output.Width == desc.InputWidth && Output.Height == desc.InputHeight)
        {
            return;
        }

        Output?.Dispose();
        Output = null!;
        Output = new(desc.InputWidth, desc.InputHeight);
    }

    protected override void Destroy()
    {
        Output?.Dispose();
        pipeline?.Dispose();
        buffer?.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 400)]
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
    public Vector4 Position;

    [FieldOffset(272)]
    public Vector2 RenderSize;

    [FieldOffset(280)]
    public Vector2 JitterOffset;

    [FieldOffset(288)]
    public uint FrameIndex;

    [FieldOffset(296)]
    public ResourceHandle Scene;

    [FieldOffset(304)]
    public ResourceHandle Vertices;

    [FieldOffset(312)]
    public ResourceHandle Indices;

    [FieldOffset(320)]
    public ResourceHandle Materials;

    [FieldOffset(328)]
    public ResourceHandle Instances;

    [FieldOffset(336)]
    public ResourceHandle Color;

    [FieldOffset(344)]
    public ResourceHandle Depth;

    [FieldOffset(352)]
    public ResourceHandle MotionVectors;

    [FieldOffset(360)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(368)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(376)]
    public ResourceHandle Normals;

    [FieldOffset(384)]
    public ResourceHandle Roughness;

    [FieldOffset(392)]
    public ResourceHandle SpecularHitDistance;
}
