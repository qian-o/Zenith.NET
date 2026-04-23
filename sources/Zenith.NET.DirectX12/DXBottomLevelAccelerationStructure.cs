using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public DXBottomLevelAccelerationStructure(DXGraphicsContext context, BottomLevelAccelerationStructureDesc desc, DXCommandBuffer commandBuffer) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        uint geometryCount = (uint)desc.Geometries.Length;

        TransformBuffer = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(Matrix3X4<float>) * geometryCount),
            StrideInBytes = (uint)sizeof(Matrix3X4<float>),
            Flags = BufferUsageFlags.MapWrite
        });

        MappedMemory mappedMemory = TransformBuffer.Map();

        desc.Geometries.Select(static item => DXFormats.DirectX12(item.Triangles.Transform)).ToArray().CopyTo(new Span<Matrix3X4<float>>((Matrix3X4<float>*)mappedMemory.Pointer, (int)geometryCount));

        TransformBuffer.Unmap();

        RaytracingGeometryDesc* geometries = (RaytracingGeometryDesc*)ZenithMarshal.Allocate<RaytracingGeometryDesc>(scope, geometryCount);
        for (uint i = 0; i < geometryCount; i++)
        {
            RayTracingGeometry geometry = desc.Geometries[i];

            geometries[i] = new()
            {
                Type = DXFormats.DirectX12(geometry.Type),
                Flags = DXFormats.DirectX12(geometry.Flags),
                Anonymous = new
                (
                    triangles: geometry.Type is RayTracingGeometryType.Triangles ? new()
                    {
                        Transform3x4 = TransformBuffer.GPUVirtualAddress + (uint)(sizeof(Matrix3X4<float>) * i),
                        IndexFormat = geometry.Triangles.IndexBuffer is not null ? DXFormats.DirectX12(geometry.Triangles.IndexFormat) : Format.FormatUnknown,
                        VertexFormat = DXFormats.DirectX12(geometry.Triangles.VertexFormat),
                        IndexCount = geometry.Triangles.IndexCount,
                        VertexCount = geometry.Triangles.VertexCount,
                        IndexBuffer = geometry.Triangles.IndexBuffer is not null ? geometry.Triangles.IndexBuffer.DirectX12().GPUVirtualAddress + geometry.Triangles.IndexOffsetInBytes : 0,
                        VertexBuffer = new()
                        {
                            StartAddress = geometry.Triangles.VertexBuffer.DirectX12().GPUVirtualAddress + geometry.Triangles.VertexOffsetInBytes,
                            StrideInBytes = geometry.Triangles.VertexStrideInBytes
                        }
                    } : null,
                    aABBs: geometry.Type is RayTracingGeometryType.AABBs ? new()
                    {
                        AABBCount = geometry.AABBs.Count,
                        AABBs = new()
                        {
                            StartAddress = geometry.AABBs.Buffer.DirectX12().GPUVirtualAddress + geometry.AABBs.OffsetInBytes,
                            StrideInBytes = geometry.AABBs.StrideInBytes
                        }
                    } : null
                )
            };
        }

        BuildRaytracingAccelerationStructureInputs inputs = new()
        {
            Type = RaytracingAccelerationStructureType.BottomLevel,
            Flags = DXFormats.DirectX12(desc.Flags),
            NumDescs = geometryCount,
            PGeometryDescs = geometries
        };

        RaytracingAccelerationStructurePrebuildInfo prebuildInfo = new();

        context.Device5?.GetRaytracingAccelerationStructurePrebuildInfo(&inputs, &prebuildInfo);

        AccelerationStructureBuffer = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes,
            StrideInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes,
            Flags = BufferUsageFlags.AccelerationStructure
        });

        ScratchBuffer = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes,
            StrideInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes,
            Flags = BufferUsageFlags.UnorderedAccess
        });

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            Inputs = inputs,
            ScratchAccelerationStructureData = ScratchBuffer.GPUVirtualAddress
        };

        commandBuffer.GraphicsCommandList4.BuildRaytracingAccelerationStructure(&buildDesc, 0, (RaytracingAccelerationStructurePostbuildInfoDesc*)null);

        ResourceBarrier barrier = new()
        {
            Type = ResourceBarrierType.Uav,
            UAV = new()
            {
                PResource = AccelerationStructureBuffer.Resource
            }
        };

        commandBuffer.GraphicsCommandList4.ResourceBarrier(1, &barrier);
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXBuffer TransformBuffer { get; }

    public DXBuffer AccelerationStructureBuffer { get; }

    public DXBuffer ScratchBuffer { get; }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        ScratchBuffer.Dispose();
        AccelerationStructureBuffer.Dispose();
        TransformBuffer.Dispose();
    }
}
