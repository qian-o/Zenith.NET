using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public DXBottomLevelAccelerationStructure(DXGraphicsContext context, DXCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        Transform = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(Matrix3X4<float>) * desc.Geometries.Length),
            Residency = MemoryResidency.CpuWriteOnly
        });

        BuildRaytracingAccelerationStructureInputs inputs = Inputs(scope, desc);

        RaytracingAccelerationStructurePrebuildInfo prebuildInfo = new();
        context.Device.GetRaytracingAccelerationStructurePrebuildInfo(&inputs, &prebuildInfo);

        AccelerationStructure = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes,
            Residency = MemoryResidency.GpuOnly
        }, ResourceFlags.RaytracingAccelerationStructure);

        Scratch = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes,
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructure.GPUVirtualAddress,
            Inputs = inputs,
            ScratchAccelerationStructureData = Scratch.GPUVirtualAddress
        };

        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));
    }

    public DXBuffer Transform { get; }

    public DXBuffer AccelerationStructure { get; }

    public DXBuffer Scratch { get; }

    public void Update(DXCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc newDesc)
    {
        using ZenithMarshal.Scope scope = new();

        BuildRaytracingAccelerationStructureInputs inputs = Inputs(scope, newDesc);
        inputs.Flags |= RaytracingAccelerationStructureBuildFlags.PerformUpdate;

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructure.GPUVirtualAddress,
            Inputs = inputs,
            SourceAccelerationStructureData = AccelerationStructure.GPUVirtualAddress,
            ScratchAccelerationStructureData = Scratch.GPUVirtualAddress
        };

        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return type switch
        {
            NativeObjectType.D3D12GpuVirtualAddress => (nint)AccelerationStructure.GPUVirtualAddress,
            NativeObjectType.D3D12Resource => (nint)AccelerationStructure.Resource.Handle,
            _ => default
        };
    }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Name = name;
    }

    protected override void Destroy()
    {
        Scratch.Dispose();
        AccelerationStructure.Dispose();
        Transform.Dispose();
    }

    private BuildRaytracingAccelerationStructureInputs Inputs(ZenithMarshal.Scope scope, BottomLevelAccelerationStructureDesc desc)
    {
        uint geometryCount = (uint)desc.Geometries.Length;

        nint pointer = Transform.Map();

        Matrix3X4<float>* transforms = (Matrix3X4<float>*)pointer;
        RaytracingGeometryDesc* geometries = (RaytracingGeometryDesc*)ZenithMarshal.Allocate<RaytracingGeometryDesc>(scope, geometryCount);
        for (uint i = 0; i < geometryCount; i++)
        {
            RayTracingGeometry geometry = desc.Geometries[i];

            transforms[i] = DXFormats.DirectX12(geometry.TriangleGeometry.Transform);
            geometries[i] = new()
            {
                Type = DXFormats.DirectX12(geometry.Type),
                Flags = geometry.IsOpaque ? RaytracingGeometryFlags.Opaque : RaytracingGeometryFlags.None,
                Anonymous = new
                (
                    triangles: geometry.Type is RayTracingGeometryType.Triangle ? new()
                    {
                        Transform3x4 = Transform.GPUVirtualAddress + (ulong)(sizeof(Matrix3X4<float>) * i),
                        IndexFormat = geometry.TriangleGeometry.IndexBuffer is not null ? DXFormats.DirectX12(geometry.TriangleGeometry.IndexFormat) : Format.FormatUnknown,
                        VertexFormat = DXFormats.DirectX12(geometry.TriangleGeometry.VertexFormat),
                        IndexCount = geometry.TriangleGeometry.IndexCount,
                        VertexCount = geometry.TriangleGeometry.VertexCount,
                        IndexBuffer = geometry.TriangleGeometry.IndexBuffer is not null ? geometry.TriangleGeometry.IndexBuffer.DirectX12().GPUVirtualAddress + geometry.TriangleGeometry.IndexOffsetInBytes : 0ul,
                        VertexBuffer = new()
                        {
                            StartAddress = geometry.TriangleGeometry.VertexBuffer.DirectX12().GPUVirtualAddress + geometry.TriangleGeometry.VertexOffsetInBytes,
                            StrideInBytes = geometry.TriangleGeometry.VertexStrideInBytes
                        }
                    } : null,
                    aABBs: geometry.Type is RayTracingGeometryType.Aabb ? new()
                    {
                        AABBCount = geometry.AabbGeometry.Count,
                        AABBs = new()
                        {
                            StartAddress = geometry.AabbGeometry.Buffer.DirectX12().GPUVirtualAddress + geometry.AabbGeometry.OffsetInBytes,
                            StrideInBytes = geometry.AabbGeometry.StrideInBytes
                        }
                    } : null
                )
            };
        }

        Transform.Unmap();

        return new()
        {
            Type = RaytracingAccelerationStructureType.BottomLevel,
            Flags = DXFormats.DirectX12(desc.BuildFlags),
            NumDescs = geometryCount,
            PGeometryDescs = geometries
        };
    }
}
