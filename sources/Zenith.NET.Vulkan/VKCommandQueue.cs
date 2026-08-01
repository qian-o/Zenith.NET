using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKCommandQueue : CommandQueue
{
    public Queue Queue;

    public uint QueueFamilyIndex;

    public VKCommandQueue(VKGraphicsContext context, CommandQueueType type, Queue queue, uint queueFamilyIndex) : base(context, type)
    {
        Queue = queue;
        QueueFamilyIndex = queueFamilyIndex;

        Timeline = new VKTimeline(context, this);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override Timeline Timeline { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return type switch
        {
            NativeObjectType.VulkanQueue => Queue.Handle,
            NativeObjectType.VulkanQueueFamilyIndex => (nint)QueueFamilyIndex,
            _ => default
        };
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new VKCommandBuffer(Context, this);
    }

    protected override double GetTimestampPeriod(out uint validBits)
    {
        PhysicalDeviceProperties properties;
        Context.Vk.GetPhysicalDeviceProperties(Context.PhysicalDevice, &properties);

        uint queueFamilyCount = 0;
        Context.Vk.GetPhysicalDeviceQueueFamilyProperties(Context.PhysicalDevice, &queueFamilyCount, default);

        QueueFamilyProperties* queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        Context.Vk.GetPhysicalDeviceQueueFamilyProperties(Context.PhysicalDevice, &queueFamilyCount, queueFamilies);

        validBits = queueFamilies[(int)QueueFamilyIndex].TimestampValidBits;

        return properties.Limits.TimestampPeriod;
    }

    protected override void SubmitImpl(ReadOnlySpan<TimelineValue> waits, CommandBuffer commandBuffer)
    {
        SemaphoreSubmitInfo* waitSemaphoreInfos = stackalloc SemaphoreSubmitInfo[waits.Length];
        for (int i = 0; i < waits.Length; i++)
        {
            TimelineValue wait = waits[i];

            waitSemaphoreInfos[i] = new()
            {
                SType = StructureType.SemaphoreSubmitInfo,
                Semaphore = wait.Timeline.Vulkan().Semaphore,
                Value = wait.Value,
                StageMask = PipelineStageFlags2.AllCommandsBit
            };
        }

        CommandBufferSubmitInfo commandBufferInfo = new()
        {
            SType = StructureType.CommandBufferSubmitInfo,
            CommandBuffer = commandBuffer.Vulkan().CommandBuffer
        };

        SubmitInfo2 submitInfo = new()
        {
            SType = StructureType.SubmitInfo2,
            WaitSemaphoreInfoCount = (uint)waits.Length,
            PWaitSemaphoreInfos = waitSemaphoreInfos,
            CommandBufferInfoCount = 1,
            PCommandBufferInfos = &commandBufferInfo
        };

        Context.Vk.QueueSubmit2(Queue, 1, &submitInfo, default).Success();
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Queue,
            ObjectHandle = (ulong)Queue.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }
}
