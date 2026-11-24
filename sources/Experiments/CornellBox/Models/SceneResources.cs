using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Models;

internal unsafe class SceneResources : DisposableObject
{
    private readonly Buffer materialBuffer;
    private readonly Buffer instanceBuffer;
    private readonly BottomLevelAccelerationStructure[] bottomLevels;
    private readonly TopLevelAccelerationStructure topLevel;
    private readonly SceneInstance[] instances;
    private readonly DrawBatch[] drawBatches;
    private readonly Matrix4x4[] dynamicTransforms = new Matrix4x4[CornellBoxGeometry.DynamicInstanceCount];
    private readonly int firstDynamicSlot;

    private bool hasSettledPause;

    public SceneResources()
    {
        CornellBoxGeometry.CreateScene(out SceneVertex[] vertices,
                                       out uint[] indices,
                                       out (uint FirstIndex, uint IndexCount, uint VertexBound)[] meshes,
                                       out (int MeshIndex, Matrix4x4 Transform, bool IsDynamic, MaterialId MaterialOverride, byte VisibilityMask)[] placements,
                                       out firstDynamicSlot,
                                       out SceneMaterial[] materials);

        instances = new SceneInstance[placements.Length];
        CornellBoxGeometry.WriteTransforms(0.0f, dynamicTransforms);

        for (int i = 0; i < placements.Length; i++)
        {
            (int meshIndex, Matrix4x4 transform, bool isDynamic, MaterialId materialOverride, _) = placements[i];
            Matrix4x4 initialTransform = i >= firstDynamicSlot ? dynamicTransforms[i - firstDynamicSlot] : transform;

            instances[i] = new()
            {
                ObjectToWorld = initialTransform,
                PreviousObjectToWorld = initialTransform,
                FirstIndex = meshes[meshIndex].FirstIndex,
                IsDynamic = isDynamic ? 1u : 0u,
                MaterialOverride = (uint)materialOverride
            };
        }

        List<DrawBatch> batchList = [];
        int batchStart = 0;
        while (batchStart < placements.Length)
        {
            int meshIndex = placements[batchStart].MeshIndex;
            int batchEnd = batchStart + 1;
            while (batchEnd < placements.Length && placements[batchEnd].MeshIndex == meshIndex)
            {
                batchEnd++;
            }

            batchList.Add(new(meshes[meshIndex].FirstIndex, meshes[meshIndex].IndexCount, (uint)batchStart, (uint)(batchEnd - batchStart)));
            batchStart = batchEnd;
        }

        drawBatches = [.. batchList];

        CommandBuffer uploadCommandBuffer = App.Context.TransferQueue.CommandBuffer();
        VertexBuffer = CreateBuffer(uploadCommandBuffer, vertices, BufferUsages.Vertex | BufferUsages.StorageReadOnly);
        IndexBuffer = CreateBuffer(uploadCommandBuffer, indices, BufferUsages.Index | BufferUsages.StorageReadOnly);
        materialBuffer = CreateBuffer(uploadCommandBuffer, materials, BufferUsages.StorageReadOnly);
        instanceBuffer = CreateBuffer(uploadCommandBuffer, instances, BufferUsages.StorageReadOnly);
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
                        VertexBuffer = VertexBuffer,
                        VertexCount = meshes[i].VertexBound,
                        VertexStrideInBytes = (uint)sizeof(SceneVertex),
                        IndexBuffer = IndexBuffer,
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

    public Buffer VertexBuffer { get; }

    public Buffer IndexBuffer { get; }

    public ReadOnlySpan<DrawBatch> DrawBatches => drawBatches;

    public uint FirstDynamicInstance => (uint)firstDynamicSlot;

    public ResourceHandle AccelerationStructure => topLevel.Handle;

    public ResourceHandle Vertices => VertexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Indices => IndexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Materials => materialBuffer.StorageReadOnlyHandle;

    public ResourceHandle Instances => instanceBuffer.StorageReadOnlyHandle;

    public void Update(CommandBuffer commandBuffer, double elapsedSeconds, bool isPaused, bool reset)
    {
        if (isPaused && hasSettledPause && !reset)
        {
            return;
        }

        hasSettledPause = isPaused;

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

        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.VertexShading | BarrierStages.ComputeShading);
        commandBuffer.UpdateAccelerationStructure(topLevel, topLevel.Desc);
    }

    protected override void Destroy()
    {
        topLevel?.Dispose();

        if (bottomLevels is not null)
        {
            for (int i = bottomLevels.Length - 1; i >= 0; i--)
            {
                bottomLevels[i]?.Dispose();
            }
        }

        instanceBuffer?.Dispose();
        materialBuffer?.Dispose();
        IndexBuffer?.Dispose();
        VertexBuffer?.Dispose();
    }

    private static Buffer CreateBuffer<T>(CommandBuffer commandBuffer, ReadOnlySpan<T> data, BufferUsages usages) where T : unmanaged
    {
        uint sizeInBytes = (uint)(sizeof(T) * data.Length);
        Buffer buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = (uint)sizeof(T),
            Usages = usages | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (T* pointer = data)
        {
            commandBuffer.Upload(buffer, 0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = sizeInBytes
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
