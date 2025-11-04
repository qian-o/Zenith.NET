using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKCommandQueue(VKGraphicsContext context, CommandQueueType type, Queue queue) : CommandQueue(context, type)
{
    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override CommandBuffer CreateCommandBuffer()
    {
        throw new NotImplementedException();
    }

    protected override void WaitIdleImpl()
    {
        Context.Vk.QueueWaitIdle(queue).Success();
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Queue,
            ObjectHandle = (ulong)queue.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }
}
