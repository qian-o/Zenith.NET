using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public DXBottomLevelAccelerationStructure(DXGraphicsContext context, DXCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        TransformBuffer = new(context, new() { SizeInBytes = (uint)(sizeof(Matrix3X4<float>) * desc.Geometries.Length), Residency = MemoryResidency.CpuWriteOnly }, default);

        FillInputs(scope, desc, out BuildRaytracingAccelerationStructureInputs inputs);

        RaytracingAccelerationStructurePrebuildInfo prebuildInfo = new();
        context.Device.GetRaytracingAccelerationStructurePrebuildInfo(&inputs, &prebuildInfo);

        AccelerationStructureBuffer = new(context, new() { SizeInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes, Usages = BufferUsages.AccelerationStructure, Residency = MemoryResidency.GpuOnly }, default);

        ScratchBuffer = new(context, new() { SizeInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes, Usages = BufferUsages.StorageReadWrite, Residency = MemoryResidency.GpuOnly }, default);

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            Inputs = inputs,
            ScratchAccelerationStructureData = ScratchBuffer.GPUVirtualAddress
        };

        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));
    }

    public DXBuffer TransformBuffer { get; }

    public DXBuffer AccelerationStructureBuffer { get; }

    public DXBuffer ScratchBuffer { get; }

    public void Update(DXCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc newDesc)
    {
        using ZenithMarshal.Scope scope = new();

        FillInputs(scope, newDesc, out BuildRaytracingAccelerationStructureInputs inputs);

        inputs.Flags |= RaytracingAccelerationStructureBuildFlags.PerformUpdate;

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            Inputs = inputs,
            SourceAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            ScratchAccelerationStructureData = ScratchBuffer.GPUVirtualAddress
        };

        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        ScratchBuffer.Dispose();
        AccelerationStructureBuffer.Dispose();
        TransformBuffer.Dispose();
    }

    private void FillInputs(ZenithMarshal.Scope scope, BottomLevelAccelerationStructureDesc desc, out BuildRaytracingAccelerationStructureInputs inputs)
    {
        uint geometryCount = (uint)desc.Geometries.Length;

        MappedMemory mappedMemory = TransformBuffer.Map();

        desc.Geometries.Select(static item => DXFormats.DirectX12(item.TriangleGeometry.Transform)).ToArray().CopyTo(new Span<Matrix3X4<float>>((Matrix3X4<float>*)mappedMemory.Pointer, (int)geometryCount));

        TransformBuffer.Unmap();

        RaytracingGeometryDesc* geometries = (RaytracingGeometryDesc*)ZenithMarshal.Allocate<RaytracingGeometryDesc>(scope, geometryCount);
        for (uint i = 0; i < geometryCount; i++)
        {
            RayTracingGeometry geometry = desc.Geometries[i];

            geometries[i] = new()
            {
                Type = DXFormats.DirectX12(geometry.Type),
                Flags = geometry.IsOpaque ? RaytracingGeometryFlags.Opaque : RaytracingGeometryFlags.None
            };

            if (geometry.Type is RayTracingGeometryType.Triangle)
            {
                RayTracingTriangleGeometry triangle = geometry.TriangleGeometry;

                geometries[i].Triangles = new()
                {
                    Transform3x4 = TransformBuffer.GPUVirtualAddress + (ulong)(sizeof(Matrix3X4<float>) * i),
                    IndexFormat = triangle.IndexBuffer is not null ? DXFormats.DirectX12(triangle.IndexFormat) : Format.FormatUnknown,
                    VertexFormat = DXFormats.DirectX12(triangle.VertexFormat),
                    IndexCount = triangle.IndexCount,
                    VertexCount = triangle.VertexCount,
                    IndexBuffer = triangle.IndexBuffer is not null ? triangle.IndexBuffer.DirectX12().GPUVirtualAddress + triangle.IndexOffsetInBytes : 0,
                    VertexBuffer = new()
                    {
                        StartAddress = triangle.VertexBuffer.DirectX12().GPUVirtualAddress + triangle.VertexOffsetInBytes,
                        StrideInBytes = triangle.VertexStrideInBytes
                    }
                };
            }
            else
            {
                RayTracingAabbGeometry aabb = geometry.AabbGeometry;

                geometries[i].AABBs = new()
                {
                    AABBCount = aabb.Count,
                    AABBs = new()
                    {
                        StartAddress = aabb.Buffer.DirectX12().GPUVirtualAddress + aabb.OffsetInBytes,
                        StrideInBytes = aabb.StrideInBytes
                    }
                };
            }
        }

        inputs = new()
        {
            Type = RaytracingAccelerationStructureType.BottomLevel,
            Flags = DXFormats.DirectX12(desc.BuildFlags),
            NumDescs = geometryCount,
            DescsLayout = ElementsLayout.Array,
            PGeometryDescs = geometries
        };
    }
}
