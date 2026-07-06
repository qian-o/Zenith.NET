using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public AccelerationStructureKHR AccelerationStructure;

    public ulong DeviceAddress;

    public VKBottomLevelAccelerationStructure(VKGraphicsContext context, VKCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        Transform = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(TransformMatrixKHR) * desc.Geometries.Length),
            Residency = MemoryResidency.CpuWriteOnly
        }, BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr);

        AccelerationStructureBuildGeometryInfoKHR info = Info(scope, desc, out uint* maxPrimitiveCounts, out AccelerationStructureBuildRangeInfoKHR* buildRangeInfos);

        AccelerationStructureBuildSizesInfoKHR sizeInfo = new() { SType = StructureType.AccelerationStructureBuildSizesInfoKhr };
        context.AccelerationStructure?.GetAccelerationStructureBuildSizes(context.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, &info, maxPrimitiveCounts, &sizeInfo);

        Storage = new(context, new()
        {
            SizeInBytes = (uint)sizeInfo.AccelerationStructureSize,
            Residency = MemoryResidency.GpuOnly
        }, BufferUsageFlags.AccelerationStructureStorageBitKhr);

        Scratch = new(context, new()
        {
            SizeInBytes = (uint)sizeInfo.BuildScratchSize,
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        AccelerationStructureCreateInfoKHR createInfo = new()
        {
            SType = StructureType.AccelerationStructureCreateInfoKhr,
            Buffer = Storage.Buffer,
            Size = sizeInfo.AccelerationStructureSize,
            Type = AccelerationStructureTypeKHR.BottomLevelKhr
        };

        context.AccelerationStructure?.CreateAccelerationStructure(context.Device, &createInfo, default, out AccelerationStructure).Success();

        AccelerationStructureDeviceAddressInfoKHR deviceAddressInfo = new()
        {
            SType = StructureType.AccelerationStructureDeviceAddressInfoKhr,
            AccelerationStructure = AccelerationStructure
        };

        DeviceAddress = context.AccelerationStructure?.GetAccelerationStructureDeviceAddress(context.Device, &deviceAddressInfo) ?? 0;

        info.DstAccelerationStructure = AccelerationStructure;
        info.ScratchData = new() { DeviceAddress = Scratch.DeviceAddress };

        context.AccelerationStructure?.CmdBuildAccelerationStructures(commandBuffer.CommandBuffer, 1, &info, &buildRangeInfos);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKBuffer Transform { get; }

    public VKBuffer Storage { get; }

    public VKBuffer Scratch { get; }

    public void Update(VKCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc newDesc)
    {
        using ZenithMarshal.Scope scope = new();

        AccelerationStructureBuildGeometryInfoKHR info = Info(scope, newDesc, out uint* maxPrimitiveCounts, out AccelerationStructureBuildRangeInfoKHR* buildRangeInfos);
        info.Mode = BuildAccelerationStructureModeKHR.UpdateKhr;
        info.SrcAccelerationStructure = AccelerationStructure;
        info.DstAccelerationStructure = AccelerationStructure;
        info.ScratchData = new() { DeviceAddress = Scratch.DeviceAddress };

        Context.AccelerationStructure?.CmdBuildAccelerationStructures(commandBuffer.CommandBuffer, 1, &info, &buildRangeInfos);
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

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
        Context.AccelerationStructure?.DestroyAccelerationStructure(Context.Device, AccelerationStructure, default);

        Scratch.Dispose();
        Storage.Dispose();
        Transform.Dispose();
    }

    private AccelerationStructureBuildGeometryInfoKHR Info(ZenithMarshal.Scope scope, BottomLevelAccelerationStructureDesc desc, out uint* maxPrimitiveCounts, out AccelerationStructureBuildRangeInfoKHR* buildRangeInfos)
    {
        uint geometryCount = (uint)desc.Geometries.Length;

        nint pointer = Transform.Map();

        TransformMatrixKHR* transforms = (TransformMatrixKHR*)pointer;
        AccelerationStructureGeometryKHR* geometries = (AccelerationStructureGeometryKHR*)ZenithMarshal.Allocate<AccelerationStructureGeometryKHR>(scope, geometryCount);
        maxPrimitiveCounts = (uint*)ZenithMarshal.Allocate<uint>(scope, geometryCount);
        buildRangeInfos = (AccelerationStructureBuildRangeInfoKHR*)ZenithMarshal.Allocate<AccelerationStructureBuildRangeInfoKHR>(scope, geometryCount);
        for (uint i = 0; i < geometryCount; i++)
        {
            RayTracingGeometry geometry = desc.Geometries[i];

            transforms[i] = VKFormats.Vulkan(geometry.TriangleGeometry.Transform);
            geometries[i] = new()
            {
                SType = StructureType.AccelerationStructureGeometryKhr,
                GeometryType = VKFormats.Vulkan(geometry.Type),
                Geometry = new
                (
                    triangles: geometry.Type is RayTracingGeometryType.Triangle ? new()
                    {
                        SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                        VertexFormat = VKFormats.Vulkan(geometry.TriangleGeometry.VertexFormat).Format,
                        VertexData = new() { DeviceAddress = geometry.TriangleGeometry.VertexBuffer.Vulkan().DeviceAddress + geometry.TriangleGeometry.VertexOffsetInBytes },
                        VertexStride = geometry.TriangleGeometry.VertexStrideInBytes,
                        MaxVertex = geometry.TriangleGeometry.VertexCount,
                        IndexType = VKFormats.Vulkan(geometry.TriangleGeometry.IndexFormat),
                        IndexData = geometry.TriangleGeometry.IndexBuffer is not null ? new() { DeviceAddress = geometry.TriangleGeometry.IndexBuffer.Vulkan().DeviceAddress + geometry.TriangleGeometry.IndexOffsetInBytes } : default,
                        TransformData = new() { DeviceAddress = Transform.DeviceAddress + (ulong)(sizeof(TransformMatrixKHR) * i) }
                    } : null,
                    aabbs: geometry.Type is RayTracingGeometryType.Aabb ? new()
                    {
                        SType = StructureType.AccelerationStructureGeometryAabbsDataKhr,
                        Data = new() { DeviceAddress = geometry.AabbGeometry.Buffer.Vulkan().DeviceAddress + geometry.AabbGeometry.OffsetInBytes },
                        Stride = geometry.AabbGeometry.StrideInBytes
                    } : null
                ),
                Flags = geometry.IsOpaque ? GeometryFlagsKHR.OpaqueBitKhr : GeometryFlagsKHR.None
            };
            maxPrimitiveCounts[i] = geometry.Type switch
            {
                RayTracingGeometryType.Triangle => geometry.TriangleGeometry.IndexBuffer is null ? geometry.TriangleGeometry.VertexCount / 3u : geometry.TriangleGeometry.IndexCount,
                RayTracingGeometryType.Aabb => geometry.AabbGeometry.Count,
                _ => default
            };
            buildRangeInfos[i] = new() { PrimitiveCount = maxPrimitiveCounts[i] };
        }

        Transform.Unmap();

        return new()
        {
            SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
            Type = AccelerationStructureTypeKHR.BottomLevelKhr,
            Flags = VKFormats.Vulkan(desc.BuildFlags),
            Mode = BuildAccelerationStructureModeKHR.BuildKhr,
            GeometryCount = geometryCount,
            PGeometries = geometries
        };
    }
}
