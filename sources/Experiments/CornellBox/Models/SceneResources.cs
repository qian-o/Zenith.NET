using System.Numerics;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Models;

internal unsafe class SceneResources : DisposableObject
{
    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer instanceBuffer;
    private readonly BottomLevelAccelerationStructure[] bottomLevels = [];
    private readonly TopLevelAccelerationStructure topLevel;
    private readonly Instance[] instances;

    public SceneResources()
    {
        CornellBoxGeometry geometry = new();
        ReadOnlySpan<Mesh> meshes = geometry.Meshes;

        vertexBuffer = CreateBuffer(geometry.Vertices);
        indexBuffer = CreateBuffer(geometry.Indices);
        materialBuffer = CreateBuffer(geometry.Materials);

        Span<Matrix4x4> transforms = stackalloc Matrix4x4[meshes.Length];
        CornellBoxGeometry.WriteTransforms(0.0f, transforms);

        instances = new Instance[meshes.Length];

        instanceBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Instance) * instances.Length),
            StrideInBytes = (uint)sizeof(Instance),
            Usages = BufferUsages.StorageReadOnly,
            Residency = MemoryResidency.CpuWriteOnly
        });

        CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

        bottomLevels = new BottomLevelAccelerationStructure[meshes.Length];
        RayTracingInstance[] rayTracingInstances = new RayTracingInstance[meshes.Length];

        for (int i = 0; i < meshes.Length; i++)
        {
            instances[i] = new()
            {
                ObjectToWorld = transforms[i],
                PreviousObjectToWorld = transforms[i],
                FirstIndex = meshes[i].FirstIndex
            };

            bottomLevels[i] = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
            {
                Geometries =
                [
                    RayTracingGeometry.Triangles(new()
                    {
                        VertexBuffer = vertexBuffer,
                        VertexFormat = PixelFormat.R32G32B32Float,
                        VertexCount = (uint)geometry.Vertices.Length,
                        VertexStrideInBytes = (uint)sizeof(Vertex),
                        IndexBuffer = indexBuffer,
                        IndexFormat = IndexFormat.UInt32,
                        IndexCount = meshes[i].IndexCount,
                        IndexOffsetInBytes = meshes[i].FirstIndex * sizeof(uint),
                        Transform = Matrix4x4.Identity
                    }, isOpaque: true)
                ],
                BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
            });

            rayTracingInstances[i] = new()
            {
                AccelerationStructure = bottomLevels[i],
                InstanceId = (uint)i,
                VisibilityMask = 0xFF,
                Transform = transforms[i],
                Flags = RayTracingInstanceFlags.None
            };
        }

        topLevel = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
        {
            Instances = rayTracingInstances,
            BuildFlags = AccelerationStructureBuildFlags.AllowUpdate | AccelerationStructureBuildFlags.PreferFastTrace
        });

        commandBuffer.Submit().Wait();
    }

    public ResourceHandle AccelerationStructure => topLevel.Handle;

    public ResourceHandle Vertices => vertexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Indices => indexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Materials => materialBuffer.StorageReadOnlyHandle;

    public ResourceHandle Instances => instanceBuffer.StorageReadOnlyHandle;

    public void Update(CommandBuffer commandBuffer, double elapsedSeconds)
    {
        Span<Matrix4x4> transforms = stackalloc Matrix4x4[instances.Length];
        CornellBoxGeometry.WriteTransforms((float)elapsedSeconds, transforms);

        for (int i = 0; i < instances.Length; i++)
        {
            instances[i].PreviousObjectToWorld = instances[i].ObjectToWorld;
            instances[i].ObjectToWorld = transforms[i];
            topLevel.Desc.Instances[i].Transform = transforms[i];
        }

        fixed (Instance* pointer = instances)
        {
            instanceBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Instance) * instances.Length)
            });
        }

        commandBuffer.UpdateAccelerationStructure(topLevel, topLevel.Desc);
    }

    protected override void Destroy()
    {
        topLevel?.Dispose();

        for (int i = bottomLevels.Length - 1; i >= 0; i--)
        {
            bottomLevels[i]?.Dispose();
        }

        instanceBuffer?.Dispose();
        materialBuffer?.Dispose();
        indexBuffer?.Dispose();
        vertexBuffer?.Dispose();
    }

    private static Buffer CreateBuffer<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        Buffer buffer = App.Context.CreateBuffer(BufferDesc.StorageReadOnly((uint)(sizeof(T) * data.Length), (uint)sizeof(T)));

        fixed (T* pointer = data)
        {
            buffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(T) * data.Length)
            });
        }

        return buffer;
    }
}
