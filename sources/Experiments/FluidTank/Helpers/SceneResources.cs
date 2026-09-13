using System.Numerics;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Helpers;

internal unsafe class SceneResources : IDisposable
{
    public Buffer Vertices;

    public Buffer Indices;

    public Buffer GlassVertices;

    public Buffer GlassIndices;

    public Buffer Materials;

    public TopLevelAccelerationStructure? Scene;

    public uint SceneIndexCount;

    public (Vector3 Center, Vector3 Normal)[] GlassFaces;

    private readonly BottomLevelAccelerationStructure? geometry;

    public SceneResources(GraphicsContext context)
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

        CommandBuffer uploadCommandBuffer = context.TransferQueue.CommandBuffer();
        Vertices = GraphicsHelper.LoadBuffer(context, uploadCommandBuffer, sceneVertices, BufferUsages.Vertex | BufferUsages.StorageReadOnly);
        Indices = GraphicsHelper.LoadBuffer(context, uploadCommandBuffer, sceneIndices, BufferUsages.Index | BufferUsages.StorageReadOnly);
        GlassVertices = GraphicsHelper.LoadBuffer(context, uploadCommandBuffer, glassVertices, BufferUsages.Vertex);
        GlassIndices = GraphicsHelper.LoadBuffer(context, uploadCommandBuffer, glassIndices, BufferUsages.Index);
        Materials = GraphicsHelper.LoadBuffer(context, uploadCommandBuffer, materials, BufferUsages.StorageReadOnly);
        uploadCommandBuffer.Submit().Wait();

        if (context.Capabilities.RayTracingSupported)
        {
            CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();

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

    public void Dispose()
    {
        Scene?.Dispose();
        geometry?.Dispose();
        Materials.Dispose();
        GlassIndices.Dispose();
        GlassVertices.Dispose();
        Indices.Dispose();
        Vertices.Dispose();
    }
}
