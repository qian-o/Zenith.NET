using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox;

internal unsafe class Scene : DisposableObject
{
    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer instanceBuffer;
    private readonly BottomLevelAccelerationStructure[] bottomLevels;
    private readonly TopLevelAccelerationStructure topLevel;
    private readonly Instance[] instances;
    private readonly RayTracingInstance[] rayTracingInstances;

    private float time;

    public Scene()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials, out Mesh[] meshes);

        vertexBuffer = CreateBuffer(vertices);
        indexBuffer = CreateBuffer(indices);
        materialBuffer = CreateBuffer(materials);

        Span<Matrix4x4> transforms = stackalloc Matrix4x4[meshes.Length];
        Transforms(time, transforms);

        instances = new Instance[meshes.Length];

        for (int i = 0; i < instances.Length; i++)
        {
            instances[i] = new()
            {
                ObjectToWorld = transforms[i],
                PreviousObjectToWorld = transforms[i],
                FirstIndex = meshes[i].FirstIndex
            };
        }

        instanceBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Instance) * instances.Length),
            StrideInBytes = (uint)sizeof(Instance),
            Usages = BufferUsages.StorageReadOnly,
            Residency = MemoryResidency.CpuWriteOnly
        });

        CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

        bottomLevels = new BottomLevelAccelerationStructure[meshes.Length];
        rayTracingInstances = new RayTracingInstance[meshes.Length];

        for (int i = 0; i < meshes.Length; i++)
        {
            bottomLevels[i] = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
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
                            IndexCount = meshes[i].IndexCount,
                            IndexOffsetInBytes = meshes[i].FirstIndex * sizeof(uint),
                            Transform = Matrix4x4.Identity
                        },
                        IsOpaque = true
                    }
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

        topLevel = commandBuffer.BuildAccelerationStructure(TopLevelDesc());

        commandBuffer.Submit().Wait();
    }

    public ResourceHandle AccelerationStructure => topLevel.Handle;

    public ResourceHandle Vertices => vertexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Indices => indexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Materials => materialBuffer.StorageReadOnlyHandle;

    public ResourceHandle Instances => instanceBuffer.StorageReadOnlyHandle;

    public void Update(CommandBuffer commandBuffer, double delta)
    {
        time += (float)delta;

        Span<Matrix4x4> transforms = stackalloc Matrix4x4[instances.Length];
        Transforms(time, transforms);

        for (int i = 0; i < instances.Length; i++)
        {
            instances[i].PreviousObjectToWorld = instances[i].ObjectToWorld;
            instances[i].ObjectToWorld = transforms[i];
            rayTracingInstances[i].Transform = transforms[i];
        }

        fixed (Instance* pointer = instances)
        {
            instanceBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Instance) * instances.Length)
            });
        }

        commandBuffer.UpdateAccelerationStructure(topLevel, TopLevelDesc());
    }

    protected override void Destroy()
    {
        topLevel.Dispose();

        foreach (BottomLevelAccelerationStructure bottomLevel in bottomLevels)
        {
            bottomLevel.Dispose();
        }

        instanceBuffer.Dispose();
        materialBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }

    private TopLevelAccelerationStructureDesc TopLevelDesc()
    {
        return new()
        {
            Instances = rayTracingInstances,
            BuildFlags = AccelerationStructureBuildFlags.AllowUpdate | AccelerationStructureBuildFlags.PreferFastTrace
        };
    }

    private static void Transforms(float time, Span<Matrix4x4> transforms)
    {
        Vector3 sphere = new(425.0f + (75.0f * MathF.Cos(0.8f * time)), 45.0f, 125.0f + (70.0f * MathF.Sin(0.8f * time)));

        Matrix4x4 outer = Matrix4x4.CreateRotationY(0.6f * time);
        Matrix4x4 middle = Matrix4x4.CreateRotationX(1.0f * time) * outer;
        Matrix4x4 inner = Matrix4x4.CreateRotationY(1.6f * time) * middle;
        Matrix4x4 gyroscope = Matrix4x4.CreateTranslation(185.5f, 350.0f, 169.0f);

        transforms[0] = Matrix4x4.Identity;
        transforms[1] = Matrix4x4.CreateTranslation(sphere);
        transforms[2] = outer * gyroscope;
        transforms[3] = middle * gyroscope;
        transforms[4] = inner * gyroscope;
    }

    private static Buffer CreateBuffer<T>(T[] data) where T : unmanaged
    {
        Buffer buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(T) * data.Length),
            StrideInBytes = (uint)sizeof(T),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

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
