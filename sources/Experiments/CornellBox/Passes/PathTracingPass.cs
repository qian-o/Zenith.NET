using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class PathTracingPass : Pass
{
    private const uint ThreadGroupSize = 8;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer constantBuffer;
    private readonly ComputePipeline pipeline;
    private readonly BottomLevelAccelerationStructure blas;
    private readonly TopLevelAccelerationStructure tlas;
    private readonly Buffer materialBuffer;

    public PathTracingPass()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials);

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
            StrideInBytes = (uint)sizeof(Vertex),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (Vertex* pointer = vertices)
        {
            vertexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length)
            });
        }

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (uint* pointer = indices)
        {
            indexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(uint) * indices.Length)
            });
        }

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader computeShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("PathTracing.slang"), "CSMain"));

        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = computeShader });

        CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

        blas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
        {
            Geometries =
            [
                new()
                {
                    Type = RayTracingGeometryType.Triangle,
                    TriangleGeometry = new()
                    {
                        VertexBuffer = vertexBuffer,
                        VertexFormat = PixelFormat.R32G32B32Float,
                        VertexCount = (uint)vertices.Length,
                        VertexStrideInBytes = (uint)sizeof(Vertex),
                        IndexBuffer = indexBuffer,
                        IndexFormat = IndexFormat.UInt32,
                        IndexCount = (uint)indices.Length,
                        Transform = Matrix4x4.Identity
                    },
                    IsOpaque = true
                }
            ],
            BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        tlas = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
        {
            Instances =
            [
                new()
                {
                    AccelerationStructure = blas,
                    InstanceId = 0,
                    VisibilityMask = 0xFF,
                    Transform = Matrix4x4.Identity,
                    Flags = RayTracingInstanceFlags.None
                }
            ],
            BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        commandBuffer.Submit().Wait();

        materialBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Material) * materials.Length),
            StrideInBytes = (uint)sizeof(Material),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (Material* pointer = materials)
        {
            materialBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Material) * materials.Length)
            });
        }
    }

    public Texture Color { get; private set; } = null!;

    public Texture Depth { get; private set; } = null!;

    public Texture Normal { get; private set; } = null!;

    public Texture MotionVectors { get; private set; } = null!;

    public void Render(CommandBuffer commandBuffer, CameraHandler camera, Matrix4x4 previousViewProjection, Vector2 jitter, uint frameIndex)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        Constants constants = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = view * projection,
            PreviousViewProjection = previousViewProjection,
            PositionFrame = new(camera.Position, BitConverter.UInt32BitsToSingle(frameIndex)),
            RenderSizeJitter = new(Color.Desc.Width, Color.Desc.Height, jitter.X, jitter.Y),
            Scene = tlas.Handle,
            Vertices = vertexBuffer.StorageReadOnlyHandle,
            Indices = indexBuffer.StorageReadOnlyHandle,
            Materials = materialBuffer.StorageReadOnlyHandle,
            Color = Color.StorageHandle,
            Depth = Depth.StorageHandle,
            Normal = Normal.StorageHandle,
            MotionVectors = MotionVectors.StorageHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(Depth, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(Normal, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(MotionVectors, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Dispatch((Color.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize, (Color.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(Depth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(Normal, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(MotionVectors, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public void Resize(uint width, uint height)
    {
        Color?.Dispose();
        Color = CreateTexture(width, height, PixelFormat.R16G16B16A16Float);

        Depth?.Dispose();
        Depth = CreateTexture(width, height, PixelFormat.R32Float);

        Normal?.Dispose();
        Normal = CreateTexture(width, height, PixelFormat.R16G16B16A16Float);

        MotionVectors?.Dispose();
        MotionVectors = CreateTexture(width, height, PixelFormat.R16G16Float);
    }

    protected override void Destroy()
    {
        MotionVectors?.Dispose();
        Normal?.Dispose();
        Depth?.Dispose();
        Color?.Dispose();
        materialBuffer.Dispose();
        tlas.Dispose();
        blas.Dispose();
        pipeline.Dispose();
        constantBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 352)]
file struct Constants
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
    public ResourceHandle Scene;

    [FieldOffset(296)]
    public ResourceHandle Vertices;

    [FieldOffset(304)]
    public ResourceHandle Indices;

    [FieldOffset(312)]
    public ResourceHandle Materials;

    [FieldOffset(320)]
    public ResourceHandle Color;

    [FieldOffset(328)]
    public ResourceHandle Depth;

    [FieldOffset(336)]
    public ResourceHandle Normal;

    [FieldOffset(344)]
    public ResourceHandle MotionVectors;
}
