using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class PathTracingPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private const uint ThreadGroupSize = 8;

    private Buffer vertexBuffer = null!;
    private Buffer indexBuffer = null!;
    private Buffer constantBuffer = null!;
    private ComputePipeline pipeline = null!;
    private BottomLevelAccelerationStructure blas = null!;
    private TopLevelAccelerationStructure tlas = null!;
    private Buffer materialBuffer = null!;

    public Texture Color { get; private set; } = null!;

    public Texture Depth { get; private set; } = null!;

    public Texture Normal { get; private set; } = null!;

    public Texture MotionVectors { get; private set; } = null!;

    protected override void Initialize()
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

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("PathTracing.slang"), "CSMain"));

        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

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

        Color = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);
        Depth = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R32Float);
        Normal = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);
        MotionVectors = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16Float);
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        Constants constants = new()
        {
            InverseView = args.InverseView,
            InverseProjection = args.InverseProjection,
            ViewProjection = args.ViewProjection,
            PreviousViewProjection = args.PreviousViewProjection,
            PositionFrame = new(args.CameraPosition, BitConverter.UInt32BitsToSingle(args.FrameIndex)),
            RenderSizeJitter = new(RenderWidth, RenderHeight, args.Jitter.X, args.Jitter.Y),
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
        commandBuffer.Dispatch((RenderWidth + ThreadGroupSize - 1) / ThreadGroupSize, (RenderHeight + ThreadGroupSize - 1) / ThreadGroupSize, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(Depth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(Normal, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(MotionVectors, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void ResizeImpl()
    {
        Color.Dispose();
        Color = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);

        Depth.Dispose();
        Depth = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R32Float);

        Normal.Dispose();
        Normal = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);

        MotionVectors.Dispose();
        MotionVectors = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16Float);
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
