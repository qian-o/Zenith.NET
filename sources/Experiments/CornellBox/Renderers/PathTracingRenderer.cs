using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Renderers;

internal unsafe class PathTracingRenderer : Renderer
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer constantBuffer;
    private readonly ComputePipeline pipeline;

    private readonly BottomLevelAccelerationStructure blas;
    private readonly TopLevelAccelerationStructure tlas;
    private readonly Buffer materialBuffer;
    private Texture? accumulationTexture;

    private Matrix4x4 lastView;
    private Matrix4x4 lastProjection;

    public PathTracingRenderer()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials);

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
            StrideInBytes = (uint)sizeof(Vertex),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.AccelerationStructure | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (Vertex* pointer = vertices)
        {
            vertexBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length) });
        }

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.AccelerationStructure | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (uint* pointer = indices)
        {
            indexBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(uint) * indices.Length) });
        }

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(PathTracingConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader computeShader = App.Context.CreateShader(new()
        {
            Name = "CSMain",
            CodeBytes = ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("PathTracing.slang"), "CSMain")
        });

        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = computeShader });

        CommandBuffer commandBuffer = App.Context.ComputeQueue.AcquireCommandBuffer();

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
            Usages = BufferUsages.StorageReadOnly,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (Material* pointer = materials)
        {
            materialBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(Material) * materials.Length) });
        }
    }

    public uint FrameCount { get; set; }

    public override void Update(CameraHandler camera)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;

        if (view != lastView || projection != lastProjection)
        {
            lastView = view;
            lastProjection = projection;

            FrameCount = 0;
        }

        Matrix4x4.Invert(view, out Matrix4x4 invView);
        Matrix4x4.Invert(projection, out Matrix4x4 invProjection);

        PathTracingConstants parameters = new()
        {
            InvView = invView,
            InvProjection = invProjection,
            Position = camera.Position,
            FrameCount = FrameCount,
            Width = App.Width,
            Height = App.Height,
            Scene = tlas.Handle,
            Vertices = vertexBuffer.StorageReadOnlyHandle,
            Indices = indexBuffer.StorageReadOnlyHandle,
            Materials = materialBuffer.StorageReadOnlyHandle,
            AccumulationTexture = accumulationTexture!.StorageHandle,
            OutputTexture = Color.StorageHandle
        };

        constantBuffer.Upload(0, new() { Pointer = (nint)(&parameters), SizeInBytes = (uint)sizeof(PathTracingConstants) });
    }

    public override void Render(CommandBuffer commandBuffer)
    {
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);

        commandBuffer.Dispatch((App.Width + ThreadGroupSize - 1) / ThreadGroupSize, (App.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);

        FrameCount++;
    }

    public override void Resize(uint width, uint height)
    {
        base.Resize(width, height);

        accumulationTexture?.Dispose();
        accumulationTexture = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R32G32B32A32Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        FrameCount = 0;
    }

    public override void Dispose()
    {
        base.Dispose();

        accumulationTexture?.Dispose();
        materialBuffer.Dispose();
        tlas.Dispose();
        blas.Dispose();

        pipeline.Dispose();
        constantBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 208)]
file struct PathTracingConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InvView;

    [FieldOffset(64)]
    public Matrix4x4 InvProjection;

    [FieldOffset(128)]
    public Vector3 Position;

    [FieldOffset(144)]
    public uint FrameCount;

    [FieldOffset(148)]
    public uint Width;

    [FieldOffset(152)]
    public uint Height;

    [FieldOffset(160)]
    public ResourceHandle Scene;

    [FieldOffset(168)]
    public ResourceHandle Vertices;

    [FieldOffset(176)]
    public ResourceHandle Indices;

    [FieldOffset(184)]
    public ResourceHandle Materials;

    [FieldOffset(192)]
    public ResourceHandle AccumulationTexture;

    [FieldOffset(200)]
    public ResourceHandle OutputTexture;
}
