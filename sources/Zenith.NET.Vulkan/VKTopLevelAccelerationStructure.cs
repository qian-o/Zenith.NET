using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public AccelerationStructureKHR AccelerationStructure;

    public ulong DeviceAddress;

    public VKTopLevelAccelerationStructure(VKGraphicsContext context, TopLevelAccelerationStructureDesc desc, VKCommandBuffer commandBuffer) : base(context, desc)
    {
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKBuffer InstanceBuffer { get; }

    public VKBuffer AccelerationStructureBuffer { get; }

    public VKBuffer ScratchBuffer { get; }

    public void Update(VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
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
        Context.AccelerationStructure?.DestroyAccelerationStructure(Context.Device, AccelerationStructure, null);

        ScratchBuffer.Dispose();
        AccelerationStructureBuffer.Dispose();
        InstanceBuffer.Dispose();
    }
}
