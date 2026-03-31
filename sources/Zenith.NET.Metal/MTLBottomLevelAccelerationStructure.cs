using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public MTLAccelerationStructure AccelerationStructure;

    public unsafe MTLBottomLevelAccelerationStructure(MTLGraphicsContext context, BottomLevelAccelerationStructureDesc desc, MTLCommandBuffer commandBuffer) : base(context, desc)
    {
        uint geometryCount = (uint)desc.Geometries.Length;

        TransformBuffer = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(MTLPackedFloat4x3) * geometryCount),
            StrideInBytes = (uint)sizeof(MTLPackedFloat4x3),
            Flags = BufferUsageFlags.AccelerationStructure | BufferUsageFlags.MapWrite
        });

        MappedMemory mappedMemory = TransformBuffer.Map();

        desc.Geometries.Select(static item => *(MTLPackedFloat4x3*)&item.Triangles.Transform).ToArray().CopyTo(new Span<MTLPackedFloat4x3>((MTLPackedFloat4x3*)mappedMemory.Pointer, (int)geometryCount));

        TransformBuffer.Unmap();

        MTL4AccelerationStructureGeometryDescriptor[] geometryDescriptors = new MTL4AccelerationStructureGeometryDescriptor[geometryCount];
        for (uint i = 0; i < geometryCount; i++)
        {
            RayTracingGeometry geometry = desc.Geometries[i];

            switch (geometry.Type)
            {
                case RayTracingGeometryType.Triangles:
                    geometryDescriptors[i] = new MTL4AccelerationStructureTriangleGeometryDescriptor()
                    {
                        VertexBuffer = new()
                        {
                            BufferAddress = geometry.Triangles.VertexBuffer.Metal().Buffer.GpuAddress + geometry.Triangles.VertexOffsetInBytes,
                            Length = geometry.Triangles.VertexStrideInBytes * geometry.Triangles.VertexCount
                        },
                        VertexFormat = MTLFormats.Metal(geometry.Triangles.VertexFormat).AttributeFormat,
                        VertexStride = geometry.Triangles.VertexStrideInBytes,
                        IndexBuffer = geometry.Triangles.IndexBuffer is not null ? new()
                        {
                            BufferAddress = geometry.Triangles.IndexBuffer.Metal().Buffer.GpuAddress + geometry.Triangles.IndexOffsetInBytes,
                            Length = (uint)(geometry.Triangles.IndexFormat is IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint)) * geometry.Triangles.IndexCount
                        } : default,
                        IndexType = MTLFormats.Metal(geometry.Triangles.IndexFormat),
                        TriangleCount = geometry.Triangles.IndexBuffer is not null ? geometry.Triangles.IndexCount / 3 : geometry.Triangles.VertexCount / 3,
                        TransformationMatrixBuffer = new()
                        {
                            BufferAddress = TransformBuffer.GpuAddress + (uint)(sizeof(MTLPackedFloat4x3) * i),
                            Length = (uint)sizeof(MTLPackedFloat4x3)
                        },
                        TransformationMatrixLayout = MTLMatrixLayout.RowMajor,
                        Opaque = geometry.Flags.HasFlag(RayTracingGeometryFlags.Opaque)
                    };
                    break;

                case RayTracingGeometryType.AABBs:
                    geometryDescriptors[i] = new MTL4AccelerationStructureBoundingBoxGeometryDescriptor()
                    {
                        BoundingBoxBuffer = new()
                        {
                            BufferAddress = geometry.AABBs.Buffer.Metal().Buffer.GpuAddress + geometry.AABBs.OffsetInBytes,
                            Length = geometry.AABBs.StrideInBytes * geometry.AABBs.Count
                        },
                        BoundingBoxStride = geometry.AABBs.StrideInBytes,
                        BoundingBoxCount = geometry.AABBs.Count,
                        Opaque = geometry.Flags.HasFlag(RayTracingGeometryFlags.Opaque)
                    };
                    break;
            }
        }

        MTL4PrimitiveAccelerationStructureDescriptor descriptor = new()
        {
            GeometryDescriptors = geometryDescriptors,
            Usage = MTLFormats.Metal(desc.Flags)
        };

        MTLAccelerationStructureSizes sizes = context.Device.AccelerationStructureSizes(descriptor);

        AccelerationStructure = context.Device.MakeAccelerationStructure(sizes.AccelerationStructureSize);
        context.AddAllocation(AccelerationStructure);

        ScratchBuffer = new(context, new()
        {
            SizeInBytes = (uint)sizes.BuildScratchBufferSize,
            StrideInBytes = (uint)sizes.BuildScratchBufferSize,
            Flags = BufferUsageFlags.ShaderResource
        });

        commandBuffer.CommandEncoder.Compute?.Build(AccelerationStructure, descriptor, new(ScratchBuffer.Buffer.GpuAddress, ScratchBuffer.Desc.SizeInBytes));
        commandBuffer.CommandEncoder.Compute?.BarrierAfterEncoderStages(MTLStages.AccelerationStructure, MTLStages.AccelerationStructure, MTL4VisibilityOptions.Device);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLBuffer TransformBuffer { get; }

    public MTLBuffer ScratchBuffer { get; }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Label = name;
    }

    protected override void Destroy()
    {
        Context.RemoveAllocation(AccelerationStructure);

        AccelerationStructure.Dispose();

        ScratchBuffer.Dispose();
        TransformBuffer.Dispose();
    }
}
