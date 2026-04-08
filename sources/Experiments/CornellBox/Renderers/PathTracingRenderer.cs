using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Renderers;

internal unsafe class PathTracingRenderer : Renderer
{
    private const uint ThreadGroupSize = 16;

    private static readonly ResourceBinding[] ResourceBindings =
    [
        new() { Type = ResourceType.AccelerationStructure, Count = 1 },
        new() { Type = ResourceType.ConstantBuffer, Count = 1 },
        new() { Type = ResourceType.StructuredBuffer, Count = 1 },
        new() { Type = ResourceType.StructuredBuffer, Count = 1 },
        new() { Type = ResourceType.StructuredBuffer, Count = 1 },
        new() { Type = ResourceType.TextureReadWrite, Count = 1 },
        new() { Type = ResourceType.TextureReadWrite, Count = 1 }
    ];

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer cameraBuffer;
    private readonly BottomLevelAccelerationStructure blas;
    private readonly TopLevelAccelerationStructure tlas;
    private readonly ComputePipeline pipeline;

    private Texture? accumulationTexture;
    private ResourceTable? resourceTable;

    private Matrix4x4 lastView;
    private Matrix4x4 lastProjection;

    public PathTracingRenderer()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials);

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.ShaderResource | BufferUsageFlags.AccelerationStructure
        });
        vertexBuffer.Upload(vertices, 0);

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.ShaderResource | BufferUsageFlags.AccelerationStructure
        });
        indexBuffer.Upload(indices, 0);

        materialBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Material) * materials.Length),
            StrideInBytes = (uint)sizeof(Material),
            Flags = BufferUsageFlags.ShaderResource
        });
        materialBuffer.Upload(materials, 0);

        cameraBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CameraParams),
            StrideInBytes = (uint)sizeof(CameraParams),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        blas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
        {
            Geometries =
            [
                new()
                {
                    Type = RayTracingGeometryType.Triangles,
                    Triangles = new()
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
                    Flags = RayTracingGeometryFlags.Opaque
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        tlas = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
        {
            Instances =
            [
                new()
                {
                    AccelerationStructure = blas,
                    ID = 0,
                    Mask = 0xFF,
                    Transform = Matrix4x4.Identity,
                    Flags = RayTracingInstanceFlags.None
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        commandBuffer.Submit(waitForCompletion: true);

        using Shader computeShader = App.Context.LoadShaderFromFile(ShaderPath("PathTracing.slang"), "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = computeShader,
            ResourceBindings = ResourceBindings,
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
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

        cameraBuffer.Upload<CameraParams>([new()
        {
            InvView = invView,
            InvProjection = invProjection,
            Position = camera.Position,
            FrameCount = FrameCount,
            Width = App.Width,
            Height = App.Height
        }], 0);
    }

    public override void Render(CommandBuffer commandBuffer)
    {
        if (resourceTable is null || accumulationTexture is null)
        {
            accumulationTexture = App.Context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R32G32B32A32Float,
                Width = App.Width,
                Height = App.Height,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
            });

            resourceTable = App.Context.CreateResourceTable(new() { Bindings = ResourceBindings });
            resourceTable.Write(0, tlas);
            resourceTable.Write(1, cameraBuffer);
            resourceTable.Write(2, vertexBuffer);
            resourceTable.Write(3, indexBuffer);
            resourceTable.Write(4, materialBuffer);
            resourceTable.Write(5, accumulationTexture);
            resourceTable.Write(6, Color);
        }

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceTable(resourceTable);

        commandBuffer.Dispatch((App.Width + ThreadGroupSize - 1) / ThreadGroupSize, (App.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);

        FrameCount++;
    }

    public override void Resize(uint width, uint height)
    {
        base.Resize(width, height);

        resourceTable?.Dispose();
        resourceTable = null;

        accumulationTexture?.Dispose();
        accumulationTexture = null;

        FrameCount = 0;
    }

    public override void Dispose()
    {
        base.Dispose();

        resourceTable?.Dispose();
        accumulationTexture?.Dispose();

        pipeline.Dispose();
        tlas.Dispose();
        blas.Dispose();
        cameraBuffer.Dispose();
        materialBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 160)]
file struct CameraParams
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
}
