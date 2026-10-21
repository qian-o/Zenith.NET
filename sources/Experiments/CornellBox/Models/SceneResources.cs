using System.Numerics;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Models;

internal unsafe class SceneResources : DisposableObject
{
    private static readonly Matrix4x4[] CubeTilts =
    [
        Matrix4x4.CreateRotationX(0.35f) * Matrix4x4.CreateRotationZ(0.25f),
        Matrix4x4.CreateRotationX(0.35f + 0.3f) * Matrix4x4.CreateRotationZ(0.25f + 0.2f),
        Matrix4x4.CreateRotationX(0.35f + (2 * 0.3f)) * Matrix4x4.CreateRotationZ(0.25f + (2 * 0.2f))
    ];

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer instanceBuffer;
    private readonly BottomLevelAccelerationStructure[] bottomLevels = [];
    private readonly TopLevelAccelerationStructure topLevel;
    private readonly Instance[] instances;
    private readonly RayTracingInstance[] rayTracingInstances;

    public SceneResources()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials, out Mesh[] meshes);

        vertexBuffer = CreateBuffer(vertices);
        indexBuffer = CreateBuffer(indices);
        materialBuffer = CreateBuffer(materials);

        Span<Matrix4x4> transforms = stackalloc Matrix4x4[meshes.Length];
        WriteTransforms(0.0f, transforms);

        instances = new Instance[meshes.Length];

        for (int i = 0; i < instances.Length; i++)
        {
            instances[i] = new()
            {
                ObjectToWorld = transforms[i],
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

        topLevel = commandBuffer.BuildAccelerationStructure(CreateTopLevelDesc());

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
        WriteTransforms((float)elapsedSeconds, transforms);

        for (int i = 0; i < instances.Length; i++)
        {
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

        commandBuffer.UpdateAccelerationStructure(topLevel, CreateTopLevelDesc());
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

    private TopLevelAccelerationStructureDesc CreateTopLevelDesc()
    {
        return new()
        {
            Instances = rayTracingInstances,
            BuildFlags = AccelerationStructureBuildFlags.AllowUpdate | AccelerationStructureBuildFlags.PreferFastTrace
        };
    }

    private static void WriteTransforms(float elapsedSeconds, Span<Matrix4x4> transforms)
    {
        Vector3 sphere = new(425.0f + (75.0f * MathF.Cos(0.8f * elapsedSeconds)), 45.0f, 125.0f + (70.0f * MathF.Sin(0.8f * elapsedSeconds)));

        Matrix4x4 outer = Matrix4x4.CreateRotationY(0.6f * elapsedSeconds);
        Matrix4x4 middle = Matrix4x4.CreateRotationX(1.0f * elapsedSeconds) * outer;
        Matrix4x4 inner = Matrix4x4.CreateRotationY(1.6f * elapsedSeconds) * middle;
        Matrix4x4 gyroscope = Matrix4x4.CreateTranslation(185.5f, 350.0f, 169.0f);

        transforms[0] = Matrix4x4.Identity;
        transforms[1] = Matrix4x4.CreateTranslation(sphere);
        transforms[2] = outer * gyroscope;
        transforms[3] = middle * gyroscope;
        transforms[4] = inner * gyroscope;

        Matrix4x4 cubeOrbit = Matrix4x4.CreateRotationY(0.18f * elapsedSeconds);
        Matrix4x4 sphereOrbit = Matrix4x4.CreateRotationY(-0.14f * elapsedSeconds);
        ReadOnlySpan<Vector3> cubeOffsets = [new(-60.0f, 125.0f, -15.0f), new(45.0f, 220.0f, 20.0f), new(-10.0f, 310.0f, -5.0f)];
        ReadOnlySpan<Vector3> sphereOffsets = [new(-45.0f, 125.0f, -10.0f), new(55.0f, 220.0f, 15.0f), new(5.0f, 300.0f, -5.0f)];
        for (int i = 0; i < CornellBoxGeometry.SculpturePartCount; i++)
        {
            float phase = i * 1.7f;
            float cubeBob = 8.0f * MathF.Sin((0.55f * elapsedSeconds) + phase);
            Vector3 cubePosition = Vector3.Transform(cubeOffsets[i], cubeOrbit) + new Vector3(-320.0f, cubeBob, 840.0f);
            Matrix4x4 cubeRotation = CubeTilts[i] * Matrix4x4.CreateRotationY((0.24f * elapsedSeconds) + phase);
            transforms[CornellBoxGeometry.FirstCubeMesh + i] = cubeRotation * Matrix4x4.CreateTranslation(cubePosition);

            float sphereBob = 7.0f * MathF.Sin((0.45f * elapsedSeconds) + phase + 0.8f);
            Vector3 spherePosition = Vector3.Transform(sphereOffsets[i], sphereOrbit) + new Vector3(876.0f, sphereBob, 840.0f);
            transforms[CornellBoxGeometry.FirstSphereMesh + i] = Matrix4x4.CreateTranslation(spherePosition);
        }
    }

    private static Buffer CreateBuffer<T>(ReadOnlySpan<T> data) where T : unmanaged
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
