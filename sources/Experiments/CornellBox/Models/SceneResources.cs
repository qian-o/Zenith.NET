using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Models;

internal unsafe class SceneResources : DisposableObject
{
    public static readonly Vector4 SunDirectionAndCosRadius = new(Vector3.Normalize(new(0.44f, 0.52f, -0.73f)), MathF.Cos(0.025f));
    public static readonly Vector4 SunIrradianceAndWeight = new(5.8f, 4.40f, 2.95f, 0.50f);
    public static readonly Vector4 SkyZenithAndIntensity = new(0.09f, 0.24f, 0.58f, 1.05f);
    public static readonly Vector3 SkyHorizon = new(0.50f, 0.63f, 0.78f);

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer instanceBuffer;
    private readonly Buffer lightBuffer;
    private readonly BottomLevelAccelerationStructure[] bottomLevels;
    private readonly TopLevelAccelerationStructure topLevel;
    private readonly SceneInstance[] instances;
    private readonly Matrix4x4[] transforms;

    public SceneResources()
    {
        CornellBoxGeometry.CreateScene(out SceneVertex[] vertices, out uint[] indices, out (uint FirstIndex, uint IndexCount)[] meshes, out (int MeshIndex, bool IsDynamic, uint MaterialOverride, byte VisibilityMask)[] placements, out SceneMaterial[] materials);

        SceneLight[] lights =
        [
            SceneLight.Rect(new(0.0f, 553.5f, 275.0f), Vector3.UnitX, 93.0f, Vector3.UnitZ, 83.0f, new(1.00f, 0.89f, 0.70f), 25.0f)
        ];
        LightCount = (uint)lights.Length;

        instances = new SceneInstance[placements.Length];
        transforms = new Matrix4x4[placements.Length];
        CornellBoxGeometry.WriteTransforms(0.0f, transforms);

        for (int i = 0; i < placements.Length; i++)
        {
            (int meshIndex, bool isDynamic, uint materialOverride, _) = placements[i];

            instances[i] = new()
            {
                ObjectToWorld = transforms[i],
                PreviousObjectToWorld = transforms[i],
                FirstIndex = meshes[meshIndex].FirstIndex,
                IsDynamic = isDynamic ? 1u : 0u,
                MaterialOverride = materialOverride
            };
        }

        CommandBuffer uploadCommandBuffer = App.Context.TransferQueue.CommandBuffer();
        vertexBuffer = CreateBuffer(uploadCommandBuffer, vertices);
        indexBuffer = CreateBuffer(uploadCommandBuffer, indices);
        materialBuffer = CreateBuffer(uploadCommandBuffer, materials);
        lightBuffer = CreateBuffer(uploadCommandBuffer, lights);
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
                        VertexCount = (uint)vertices.Length,
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
            (int meshIndex, _, _, byte visibilityMask) = placements[i];

            rayTracingInstances[i] = new()
            {
                AccelerationStructure = bottomLevels[meshIndex],
                InstanceId = (uint)i,
                VisibilityMask = visibilityMask,
                Transform = transforms[i],
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

    public uint LightCount { get; }

    public ResourceHandle AccelerationStructure => topLevel.Handle;

    public ResourceHandle Vertices => vertexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Indices => indexBuffer.StorageReadOnlyHandle;

    public ResourceHandle Materials => materialBuffer.StorageReadOnlyHandle;

    public ResourceHandle Instances => instanceBuffer.StorageReadOnlyHandle;

    public ResourceHandle Lights => lightBuffer.StorageReadOnlyHandle;

    public void Update(CommandBuffer commandBuffer, double elapsedSeconds, bool reset)
    {
        CornellBoxGeometry.WriteTransforms((float)elapsedSeconds, transforms);

        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i].IsDynamic is 0)
            {
                continue;
            }

            instances[i].PreviousObjectToWorld = reset ? transforms[i] : instances[i].ObjectToWorld;
            instances[i].ObjectToWorld = transforms[i];
            topLevel.Desc.Instances[i].Transform = transforms[i];
        }

        UploadInstances(commandBuffer);
        commandBuffer.UpdateAccelerationStructure(topLevel, topLevel.Desc);
    }

    public void FreezeMotion(CommandBuffer commandBuffer)
    {
        bool changed = false;
        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i].IsDynamic is not 0 && instances[i].PreviousObjectToWorld != instances[i].ObjectToWorld)
            {
                instances[i].PreviousObjectToWorld = instances[i].ObjectToWorld;
                changed = true;
            }
        }

        if (changed)
        {
            UploadInstances(commandBuffer);
        }
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

        lightBuffer?.Dispose();
        instanceBuffer?.Dispose();
        materialBuffer?.Dispose();
        indexBuffer?.Dispose();
        vertexBuffer?.Dispose();
    }

    private void UploadInstances(CommandBuffer commandBuffer)
    {
        fixed (SceneInstance* pointer = instances)
        {
            commandBuffer.Upload(instanceBuffer, 0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(SceneInstance) * instances.Length)
            });
        }

        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);
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

[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct SceneLight
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(16)]
    public Vector3 EdgeU;

    [FieldOffset(28)]
    public float HalfWidth;

    [FieldOffset(32)]
    public Vector3 EdgeV;

    [FieldOffset(44)]
    public float HalfHeight;

    [FieldOffset(48)]
    public Vector3 Radiance;

    [FieldOffset(60)]
    public float SelectionWeight;

    public static SceneLight Rect(Vector3 center, Vector3 edgeU, float halfWidth, Vector3 edgeV, float halfHeight, Vector3 color, float intensity)
    {
        Vector3 u = Vector3.Normalize(edgeU);
        Vector3 v = Vector3.Normalize(edgeV);
        Vector3 radiance = color * intensity;
        float area = 4.0f * halfWidth * halfHeight;
        float luminance = (radiance.X * 0.2126f) + (radiance.Y * 0.7152f) + (radiance.Z * 0.0722f);

        return new()
        {
            Position = center,
            EdgeU = u,
            HalfWidth = halfWidth,
            EdgeV = v,
            HalfHeight = halfHeight,
            Radiance = radiance,
            SelectionWeight = MathF.Max(luminance * area * 0.0001f, 0.05f)
        };
    }
}
