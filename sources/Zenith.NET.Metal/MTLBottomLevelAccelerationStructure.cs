using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public MTLAccelerationStructure AccelerationStructure;

    public MTLBottomLevelAccelerationStructure(MTLGraphicsContext context, MTLCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        Transform = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(MTLPackedFloat4x3) * desc.Geometries.Length),
            Residency = MemoryResidency.CpuWriteOnly
        });

        MTL4PrimitiveAccelerationStructureDescriptor descriptor = Descriptor(desc);

        MTLAccelerationStructureSizes sizes = context.Device.AccelerationStructureSizes(descriptor);

        context.Register(AccelerationStructure = context.Device.MakeAccelerationStructure(sizes.AccelerationStructureSize));

        Scratch = new(context, new()
        {
            SizeInBytes = (uint)sizes.BuildScratchBufferSize,
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        commandBuffer.Compute?.Build(AccelerationStructure, descriptor, new(Scratch.Buffer.GpuAddress, Scratch.Desc.SizeInBytes));
        commandBuffer.Compute?.BarrierAfterStages(MTLStages.AccelerationStructure, MTLStages.AccelerationStructure, MTL4VisibilityOptions.Device);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLBuffer Transform { get; }

    public MTLBuffer Scratch { get; }

    public void Update(MTLCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc newDesc)
    {
        MTL4PrimitiveAccelerationStructureDescriptor descriptor = Descriptor(newDesc);
        descriptor.Usage |= MTLAccelerationStructureUsage.Refit;

        commandBuffer.Compute?.Refit(AccelerationStructure, descriptor, AccelerationStructure, new(Scratch.Buffer.GpuAddress, Scratch.Desc.SizeInBytes));
        commandBuffer.Compute?.BarrierAfterStages(MTLStages.AccelerationStructure, MTLStages.AccelerationStructure, MTL4VisibilityOptions.Device);
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Label = name;
    }

    protected override void Destroy()
    {
        Context.Unregister(AccelerationStructure);

        Scratch.Dispose();
        Transform.Dispose();
        AccelerationStructure.Dispose();
    }

    private MTL4PrimitiveAccelerationStructureDescriptor Descriptor(BottomLevelAccelerationStructureDesc desc)
    {
        uint geometryCount = (uint)desc.Geometries.Length;

        nint pointer = Transform.Map();

        MTLPackedFloat4x3* transforms = (MTLPackedFloat4x3*)pointer;
        MTL4AccelerationStructureGeometryDescriptor[] geometries = new MTL4AccelerationStructureGeometryDescriptor[geometryCount];
        for (uint i = 0; i < geometryCount; i++)
        {
            RayTracingGeometry geometry = desc.Geometries[i];

            transforms[i] = MTLFormats.Metal(geometry.TriangleGeometry.Transform);

            switch (geometry.Type)
            {
                case RayTracingGeometryType.Triangle:
                    geometries[i] = new MTL4AccelerationStructureTriangleGeometryDescriptor()
                    {
                        VertexBuffer = new()
                        {
                            BufferAddress = geometry.TriangleGeometry.VertexBuffer.Metal().Buffer.GpuAddress + geometry.TriangleGeometry.VertexOffsetInBytes,
                            Length = geometry.TriangleGeometry.VertexStrideInBytes * geometry.TriangleGeometry.VertexCount
                        },
                        VertexFormat = MTLFormats.Metal(geometry.TriangleGeometry.VertexFormat).AttributeFormat,
                        VertexStride = geometry.TriangleGeometry.VertexStrideInBytes,
                        IndexBuffer = geometry.TriangleGeometry.IndexBuffer is not null ? new()
                        {
                            BufferAddress = geometry.TriangleGeometry.IndexBuffer.Metal().Buffer.GpuAddress + geometry.TriangleGeometry.IndexOffsetInBytes,
                            Length = (geometry.TriangleGeometry.IndexFormat is IndexFormat.UInt16 ? 2u : 4u) * geometry.TriangleGeometry.IndexCount
                        } : default,
                        IndexType = MTLFormats.Metal(geometry.TriangleGeometry.IndexFormat),
                        TriangleCount = geometry.TriangleGeometry.IndexBuffer is not null ? geometry.TriangleGeometry.IndexCount / 3 : geometry.TriangleGeometry.VertexCount / 3,
                        TransformationMatrixBuffer = new()
                        {
                            BufferAddress = Transform.Buffer.GpuAddress + (uint)(sizeof(MTLPackedFloat4x3) * i),
                            Length = (uint)sizeof(MTLPackedFloat4x3)
                        },
                        TransformationMatrixLayout = MTLMatrixLayout.RowMajor
                    };
                    break;

                case RayTracingGeometryType.Aabb:
                    geometries[i] = new MTL4AccelerationStructureBoundingBoxGeometryDescriptor()
                    {
                        BoundingBoxBuffer = new()
                        {
                            BufferAddress = geometry.AabbGeometry.Buffer.Metal().Buffer.GpuAddress + geometry.AabbGeometry.OffsetInBytes,
                            Length = geometry.AabbGeometry.StrideInBytes * geometry.AabbGeometry.Count
                        },
                        BoundingBoxStride = geometry.AabbGeometry.StrideInBytes,
                        BoundingBoxCount = geometry.AabbGeometry.Count
                    };
                    break;
            }

            geometries[i].Opaque = geometry.IsOpaque;
        }

        Transform.Unmap();

        return new()
        {
            GeometryDescriptors = geometries,
            Usage = MTLFormats.Metal(desc.BuildFlags)
        };
    }
}
