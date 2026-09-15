using System.Numerics;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Models;

internal unsafe class SceneResources : DisposableObject
{
    private readonly BottomLevelAccelerationStructure? geometry;

    public SceneResources()
    {
        FluidTankGeometry.CreateScene(out SceneVertex[] sceneVertices, out uint[] sceneIndices, out SceneMaterial[] materials);
        FluidTankGeometry.CreateGlass(out SceneVertex[] glassVertices, out uint[] glassIndices);

        SceneIndexCount = (uint)sceneIndices.Length;
        GlassFaces = new (Vector3 Center, Vector3 Normal)[glassIndices.Length / 6];

        for (int i = 0; i < GlassFaces.Length; i++)
        {
            SceneVertex first = glassVertices[glassIndices[i * 6]];
            SceneVertex opposite = glassVertices[glassIndices[(i * 6) + 2]];
            GlassFaces[i] = ((first.Position + opposite.Position) * 0.5f, first.Normal);
        }

        CommandBuffer uploadCommandBuffer = App.Context.TransferQueue.CommandBuffer();
        Vertices = LoadBuffer(uploadCommandBuffer, sceneVertices, BufferUsages.Vertex | BufferUsages.StorageReadOnly);
        Indices = LoadBuffer(uploadCommandBuffer, sceneIndices, BufferUsages.Index | BufferUsages.StorageReadOnly);
        GlassVertices = LoadBuffer(uploadCommandBuffer, glassVertices, BufferUsages.Vertex);
        GlassIndices = LoadBuffer(uploadCommandBuffer, glassIndices, BufferUsages.Index);
        Materials = LoadBuffer(uploadCommandBuffer, materials, BufferUsages.StorageReadOnly);
        uploadCommandBuffer.Submit().Wait();

        if (App.Context.Capabilities.RayTracingSupported)
        {
            CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

            geometry = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
            {
                Geometries =
                [
                    new()
                    {
                        Type = RayTracingGeometryType.Triangle,
                        TriangleGeometry = new()
                        {
                            VertexBuffer = Vertices,
                            VertexFormat = PixelFormat.R32G32B32Float,
                            VertexCount = (uint)sceneVertices.Length,
                            VertexStrideInBytes = (uint)sizeof(SceneVertex),
                            IndexBuffer = Indices,
                            IndexFormat = IndexFormat.UInt32,
                            IndexCount = SceneIndexCount,
                            Transform = Matrix4x4.Identity
                        },
                        IsOpaque = true
                    }
                ],
                BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
            });

            Scene = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
            {
                Instances =
                [
                    new()
                    {
                        AccelerationStructure = geometry,
                        InstanceId = 0,
                        VisibilityMask = 0xFF,
                        Transform = Matrix4x4.Identity,
                        Flags = RayTracingInstanceFlags.None
                    }
                ],
                BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
            });

            commandBuffer.Submit().Wait();
        }
    }

    public uint SceneIndexCount { get; }

    public Buffer Vertices { get; }

    public Buffer Indices { get; }

    public Buffer GlassVertices { get; }

    public Buffer GlassIndices { get; }

    public Buffer Materials { get; }

    public TopLevelAccelerationStructure? Scene { get; }

    public (Vector3 Center, Vector3 Normal)[] GlassFaces { get; }

    protected override void Destroy()
    {
        Scene?.Dispose();
        geometry?.Dispose();
        Materials.Dispose();
        GlassIndices.Dispose();
        GlassVertices.Dispose();
        Indices.Dispose();
        Vertices.Dispose();
    }

    private static Buffer LoadBuffer<T>(CommandBuffer commandBuffer, T[] data, BufferUsages usages) where T : unmanaged
    {
        Buffer buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(T) * data.Length),
            StrideInBytes = (uint)sizeof(T),
            Usages = usages | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (T* pointer = data)
        {
            commandBuffer.Upload(buffer, 0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(T) * data.Length)
            });
        }

        return buffer;
    }
}
