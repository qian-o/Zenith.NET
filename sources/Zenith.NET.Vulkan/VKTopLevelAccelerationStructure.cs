using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public AccelerationStructureKHR AccelerationStructure;

    public VKTopLevelAccelerationStructure(VKGraphicsContext context, VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc desc) : base(context, desc)
    {
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKBuffer Transform { get; }

    public VKBuffer Storage { get; }

    public VKBuffer Scratch { get; }

    public override ResourceHandle Handle { get; }

    public void Update(VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
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
}
