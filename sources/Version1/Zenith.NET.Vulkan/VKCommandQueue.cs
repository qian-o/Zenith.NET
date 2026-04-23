using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKCommandQueue(VKGraphicsContext context, CommandQueueType type, Queue queue, uint queueFamilyIndex) : CommandQueue(context, type)
{
    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public uint QueueFamilyIndex => queueFamilyIndex;

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new VKCommandBuffer(Context, this);
    }

    protected override void WaitIdleImpl()
    {
        Context.Vk.QueueWaitIdle(queue).Success();
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        VKCommandBuffer vkCommandBuffer = commandBuffer.Vulkan();

        fixed (VkCommandBuffer* pCommandBuffers = &vkCommandBuffer.CommandBuffer)
        {
            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = pCommandBuffers
            };

            Context.Vk.QueueSubmit(queue, 1, &submitInfo, default).Success();
        }
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
