using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public AccelerationStructureKHR AccelerationStructure;

    public ulong DeviceAddress;

    public VKBottomLevelAccelerationStructure(VKGraphicsContext context, BottomLevelAccelerationStructureDesc desc, VKCommandBuffer commandBuffer) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        uint geometryCount = (uint)desc.Geometries.Length;

        TransformBuffer = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(TransformMatrixKHR) * geometryCount),
            StrideInBytes = (uint)sizeof(TransformMatrixKHR),
            Flags = BufferUsageFlags.AccelerationStructure | BufferUsageFlags.MapWrite
        });

        MappedMemory mappedMemory = TransformBuffer.Map();

        desc.Geometries.Select(static item => *(TransformMatrixKHR*)&item.Triangles.Transform).ToArray().CopyTo(new Span<TransformMatrixKHR>((TransformMatrixKHR*)mappedMemory.Pointer, (int)geometryCount));

        TransformBuffer.Unmap();

        AccelerationStructureGeometryKHR* geometries = (AccelerationStructureGeometryKHR*)ZenithMarshal.Allocate<AccelerationStructureGeometryKHR>(scope, geometryCount);
        uint* maxPrimitiveCounts = (uint*)ZenithMarshal.Allocate<uint>(scope, geometryCount);
        AccelerationStructureBuildRangeInfoKHR* buildRangeInfos = (AccelerationStructureBuildRangeInfoKHR*)ZenithMarshal.Allocate<AccelerationStructureBuildRangeInfoKHR>(scope, geometryCount);
        for (uint i = 0; i < geometryCount; i++)
        {
            RayTracingGeometry geometry = desc.Geometries[i];

            geometries[i] = new()
            {
                SType = StructureType.AccelerationStructureGeometryKhr,
                GeometryType = VKFormats.Vulkan(geometry.Type),
                Geometry = new
                (
                    triangles: geometry.Type is RayTracingGeometryType.Triangles ? new()
                    {
                        SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                        VertexFormat = VKFormats.Vulkan(geometry.Triangles.VertexFormat),
                        VertexData = new() { DeviceAddress = geometry.Triangles.VertexBuffer.Vulkan().DeviceAddress + geometry.Triangles.VertexOffsetInBytes },
                        VertexStride = geometry.Triangles.VertexStrideInBytes,
                        MaxVertex = geometry.Triangles.VertexCount,
                        IndexType = geometry.Triangles.IndexBuffer is not null ? VKFormats.Vulkan(geometry.Triangles.IndexFormat) : IndexType.NoneKhr,
                        IndexData = new() { DeviceAddress = geometry.Triangles.IndexBuffer is not null ? geometry.Triangles.IndexBuffer.Vulkan().DeviceAddress + geometry.Triangles.IndexOffsetInBytes : 0 },
                        TransformData = new() { DeviceAddress = TransformBuffer.DeviceAddress + (uint)(sizeof(TransformMatrixKHR) * i) }
                    } : null,
                    aabbs: geometry.Type is RayTracingGeometryType.AABBs ? new()
                    {
                        SType = StructureType.AccelerationStructureGeometryAabbsDataKhr,
                        Data = new() { DeviceAddress = geometry.AABBs.Buffer.Vulkan().DeviceAddress + geometry.AABBs.OffsetInBytes },
                        Stride = geometry.AABBs.StrideInBytes
                    } : null
                ),
                Flags = VKFormats.Vulkan(geometry.Flags)
            };
            maxPrimitiveCounts[i] = geometry.Type is RayTracingGeometryType.Triangles ? geometry.Triangles.IndexBuffer is not null ? geometry.Triangles.IndexCount / 3 : geometry.Triangles.VertexCount / 3 : geometry.AABBs.Count;
            buildRangeInfos[i] = new() { PrimitiveCount = maxPrimitiveCounts[i] };
        }

        AccelerationStructureBuildGeometryInfoKHR buildInfo = new()
        {
            SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
            Type = AccelerationStructureTypeKHR.BottomLevelKhr,
            Flags = VKFormats.Vulkan(desc.Flags),
            Mode = BuildAccelerationStructureModeKHR.BuildKhr,
            GeometryCount = geometryCount,
            PGeometries = geometries
        };

        AccelerationStructureBuildSizesInfoKHR sizeInfo = new() { SType = StructureType.AccelerationStructureBuildSizesInfoKhr };

        context.AccelerationStructure?.GetAccelerationStructureBuildSizes(context.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, &buildInfo, maxPrimitiveCounts, &sizeInfo);

        BufferDesc accelerationStructureBufferDesc = new()
        {
            SizeInBytes = (uint)sizeInfo.AccelerationStructureSize,
            StrideInBytes = (uint)sizeInfo.AccelerationStructureSize
        };

        AccelerationStructureBuffer = new(context, accelerationStructureBufferDesc, VkBufferUsageFlags.AccelerationStructureStorageBitKhr);

        AccelerationStructureCreateInfoKHR createInfo = new()
        {
            SType = StructureType.AccelerationStructureCreateInfoKhr,
            Buffer = AccelerationStructureBuffer.Buffer,
            Size = sizeInfo.AccelerationStructureSize,
            Type = AccelerationStructureTypeKHR.BottomLevelKhr
        };

        context.AccelerationStructure?.CreateAccelerationStructure(context.Device, &createInfo, null, out AccelerationStructure).Success();

        AccelerationStructureDeviceAddressInfoKHR addressInfo = new()
        {
            SType = StructureType.AccelerationStructureDeviceAddressInfoKhr,
            AccelerationStructure = AccelerationStructure
        };

        DeviceAddress = context.AccelerationStructure?.GetAccelerationStructureDeviceAddress(context.Device, &addressInfo) ?? 0;

        ScratchBuffer = new(context, new()
        {
            SizeInBytes = (uint)sizeInfo.BuildScratchSize,
            StrideInBytes = (uint)sizeInfo.BuildScratchSize,
            Flags = BufferUsageFlags.ShaderResource
        });

        buildInfo.DstAccelerationStructure = AccelerationStructure;
        buildInfo.ScratchData = new() { DeviceAddress = ScratchBuffer.DeviceAddress };

        context.AccelerationStructure?.CmdBuildAccelerationStructures(commandBuffer.CommandBuffer, 1, &buildInfo, &buildRangeInfos);

        MemoryBarrier barrier = new()
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr,
            DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr
        };

        context.Vk.CmdPipelineBarrier(commandBuffer.CommandBuffer,
                                      PipelineStageFlags.AccelerationStructureBuildBitKhr,
                                      PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.RayTracingShaderBitKhr,
                                      0,
                                      1,
                                      &barrier,
                                      0,
                                      null,
                                      0,
                                      null);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKBuffer TransformBuffer { get; }

    public VKBuffer AccelerationStructureBuffer { get; }

    public VKBuffer ScratchBuffer { get; }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.AccelerationStructureKhr,
            ObjectHandle = AccelerationStructure.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.AccelerationStructure?.DestroyAccelerationStructure(Context.Device, AccelerationStructure, null);

        ScratchBuffer.Dispose();
        AccelerationStructureBuffer.Dispose();
        TransformBuffer.Dispose();
    }
}
