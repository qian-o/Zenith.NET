using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public AccelerationStructureKHR AccelerationStructure;

    public ulong DeviceAddress;

    public VKBottomLevelAccelerationStructure(VKGraphicsContext context, BottomLevelAccelerationStructureDesc desc, VKCommandBuffer commandBuffer) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        uint geometryCount = (uint)desc.Geometries.Length;

        BufferDesc transformBufferDesc = new()
        {
            SizeInBytes = (uint)(sizeof(TransformMatrixKHR) * geometryCount),
            StrideInBytes = (uint)sizeof(TransformMatrixKHR),
            Flags = BufferUsageFlags.Dynamic
        };

        TransformBuffer = new(context, transformBufferDesc, VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr);

        MappedMemory mappedMemory = TransformBuffer.Map();

        desc.Geometries.Select(static item => *(TransformMatrixKHR*)&item.Triangles.Transform).ToArray().CopyTo(new Span<TransformMatrixKHR>((TransformMatrixKHR*)mappedMemory.Pointer, (int)geometryCount));

        TransformBuffer.Unmap();

        AccelerationStructureGeometryKHR* geometries = (AccelerationStructureGeometryKHR*)ZenithMarshal.Allocate<AccelerationStructureGeometryKHR>(scope, geometryCount);
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
