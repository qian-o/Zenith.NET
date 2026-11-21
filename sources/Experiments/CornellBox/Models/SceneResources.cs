using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Models;

internal unsafe class SceneResources : DisposableObject
{
    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer instanceBuffer;
    private readonly BottomLevelAccelerationStructure[] bottomLevels;
    private readonly TopLevelAccelerationStructure topLevel;
    private readonly SceneInstance[] instances;
    private readonly Matrix4x4[] dynamicTransforms = new Matrix4x4[CornellBoxGeometry.DynamicInstanceCount];
    private readonly int firstDynamicSlot;

    private bool wasPaused;

    public SceneResources()
    {
        CornellBoxGeometry.CreateScene(out SceneVertex[] vertices,
                                       out uint[] indices,
                                       out (uint FirstIndex, uint IndexCount, uint VertexBound)[] meshes,
                                       out (int MeshIndex, Matrix4x4 Transform, bool IsDynamic, uint MaterialOverride, byte VisibilityMask)[] placements,
                                       out firstDynamicSlot,
                                       out SceneMaterial[] materials);

        instances = new SceneInstance[placements.Length];
        CornellBoxGeometry.WriteTransforms(0.0f, dynamicTransforms);

        for (int i = 0; i < placements.Length; i++)
        {
            (int meshIndex, Matrix4x4 transform, bool isDynamic, uint materialOverride, _) = placements[i];
            Matrix4x4 initialTransform = i >= firstDynamicSlot ? dynamicTransforms[i - firstDynamicSlot] : transform;

            instances[i] = new()
            {
                ObjectToWorld = initialTransform,
                PreviousObjectToWorld = initialTransform,
                FirstIndex = meshes[meshIndex].FirstIndex,
                IsDynamic = isDynamic ? 1u : 0u,
                MaterialOverride = materialOverride
            };
        }

        CommandBuffer uploadCommandBuffer = App.Context.TransferQueue.CommandBuffer();
        vertexBuffer = CreateBuffer(uploadCommandBuffer, vertices);
        indexBuffer = CreateBuffer(uploadCommandBuffer, indices);
        materialBuffer = CreateBuffer(uploadCommandBuffer, materials);
        instanceBuffer = CreateBuffer(uploadCommandBuffer, instances);
        uploadCommandBuffer.Submit().Wait();

        CommandBuffer buildCommandBuffer = App.Context.GraphicsQueue.CommandBuffer();

        bottomLevels = new BottomLevelAccelerationStructure[meshes.Length];
        for (int i = 0; i < meshes.Length; i++)
        {
            bottomLevels[i] = buildCommandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
            {
                Geometries =
                [
                    RayTracingGeometry.Triangles(new()
                    {
                        VertexBuffer = vertexBuffer,
                        VertexCount = meshes[i].VertexBound,
                        VertexStrideInBytes = (uint)sizeof(SceneVertex),
                        IndexBuffer = indexBuffer,
                        IndexFormat = IndexFormat.UInt32,
                        IndexCount = meshes[i].IndexCount,
                        IndexOffsetInBytes = meshes[i].FirstIndex * sizeof(uint),
                        Transform = Matrix4x4.Identity
                    }, isOpaque: true)
                ],
                BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
            });
        }

        RayTracingInstance[] rayTracingInstances = new RayTracingInstance[placements.Length];
        for (int i = 0; i < placements.Length; i++)
        {
            (int meshIndex, _, _, _, byte visibilityMask) = placements[i];

            rayTracingInstances[i] = new()
            {
                AccelerationStructure = bottomLevels[meshIndex],
                InstanceId = (uint)i,
                VisibilityMask = visibilityMask,
                Transform = instances[i].ObjectToWorld,
                Flags = RayTracingInstanceFlags.None
            };
        }

        topLevel = buildCommandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
        {
            Instances = rayTracingInstances,
            BuildFlags = AccelerationStructureBuildFlags.AllowUpdate | AccelerationStructureBuildFlags.PreferFastTrace
        });

        buildCommandBuffer.Submit().Wait();
    }

    public ResourceHandle AccelerationStructure => topLevel.Handle;

    public ResourceHandle Vertices => vertexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Indices => indexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Materials => materialBuffer.StorageReadOnlyHandle;

    public ResourceHandle Instances => instanceBuffer.StorageReadOnlyHandle;

    public void Update(CommandBuffer commandBuffer, double elapsedSeconds, bool isPaused, bool reset)
    {
        if (isPaused && wasPaused && !reset)
        {
            return;
        }

        wasPaused = isPaused;

        CornellBoxGeometry.WriteTransforms((float)elapsedSeconds, dynamicTransforms);

        for (int i = 0; i < CornellBoxGeometry.DynamicInstanceCount; i++)
        {
            int slot = firstDynamicSlot + i;
            Matrix4x4 transform = dynamicTransforms[i];

            instances[slot].PreviousObjectToWorld = reset ? transform : instances[slot].ObjectToWorld;
            instances[slot].ObjectToWorld = transform;
            topLevel.Desc.Instances[slot].Transform = transform;
        }

        fixed (SceneInstance* pointer = &instances[firstDynamicSlot])
        {
            commandBuffer.Upload(instanceBuffer, (uint)(firstDynamicSlot * sizeof(SceneInstance)), new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(CornellBoxGeometry.DynamicInstanceCount * sizeof(SceneInstance))
            });
        }

        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);
        commandBuffer.UpdateAccelerationStructure(topLevel, topLevel.Desc);
    }

    protected override void Destroy()
    {
        topLevel?.Dispose();

        if (bottomLevels is not null)
        {
            foreach (BottomLevelAccelerationStructure bottomLevel in bottomLevels)
            {
                bottomLevel?.Dispose();
            }
        }

        instanceBuffer?.Dispose();
        materialBuffer?.Dispose();
        indexBuffer?.Dispose();
        vertexBuffer?.Dispose();
    }

    private static Buffer CreateBuffer<T>(CommandBuffer commandBuffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        Buffer buffer = App.Context.CreateBuffer(BufferDesc.StorageReadOnly((uint)(sizeof(T) * data.Length), (uint)sizeof(T)));

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

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct SceneInstance
    {
        [FieldOffset(0)]
        public Matrix4x4 ObjectToWorld;

        [FieldOffset(64)]
        public Matrix4x4 PreviousObjectToWorld;

        [FieldOffset(128)]
        public uint FirstIndex;

        [FieldOffset(132)]
        public uint IsDynamic;

        [FieldOffset(136)]
        public uint MaterialOverride;
    }
}
