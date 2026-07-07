using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKCommandQueue : CommandQueue
{
    public Queue Queue;

    public uint QueueFamilyIndex;

    public VkSemaphore Semaphore;

    public VKCommandQueue(VKGraphicsContext context, CommandQueueType type, Queue queue, uint queueFamilyIndex) : base(context, type)
    {
        Queue = queue;
        QueueFamilyIndex = queueFamilyIndex;

        SemaphoreCreateInfo createInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        createInfo.AddNext(out SemaphoreTypeCreateInfo typeCreateInfo);
        typeCreateInfo.SemaphoreType = SemaphoreType.Timeline;

        context.Vk.CreateSemaphore(context.Device, &createInfo, default, out Semaphore).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override ulong GetCompletedValue()
    {
        ulong value;
        Context.Vk.GetSemaphoreCounterValue(Context.Device, Semaphore, &value).Success();

        return value;
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new VKCommandBuffer(Context, this);
    }

    protected override void SignalImpl(ulong signalValue)
    {
        SemaphoreSubmitInfo signalSemaphoreInfo = new()
        {
            SType = StructureType.SemaphoreSubmitInfo,
            Semaphore = Semaphore,
            Value = signalValue,
            StageMask = PipelineStageFlags2.AllCommandsBit
        };

        SubmitInfo2 submitInfo = new()
        {
            SType = StructureType.SubmitInfo2,
            SignalSemaphoreInfoCount = 1,
            PSignalSemaphoreInfos = &signalSemaphoreInfo
        };

        Context.Vk.QueueSubmit2(Queue, 1, &submitInfo, default).Success();
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        CommandBufferSubmitInfo commandBufferInfo = new()
        {
            SType = StructureType.CommandBufferSubmitInfo,
            CommandBuffer = commandBuffer.Vulkan().CommandBuffer
        };

        SubmitInfo2 submitInfo = new()
        {
            SType = StructureType.SubmitInfo2,
            CommandBufferInfoCount = 1,
            PCommandBufferInfos = &commandBufferInfo
        };

        Context.Vk.QueueSubmit2(Queue, 1, &submitInfo, default).Success();
    }

    protected override void WaitImpl(ulong waitValue)
    {
        fixed (VkSemaphore* pSemaphores = &Semaphore)
        {
            SemaphoreWaitInfo waitInfo = new()
            {
                SType = StructureType.SemaphoreWaitInfo,
                SemaphoreCount = 1,
                PSemaphores = pSemaphores,
                PValues = &waitValue
            };

            Context.Vk.WaitSemaphores(Context.Device, &waitInfo, ulong.MaxValue).Success();
        }
    }

    protected override void InsertWaitsImpl(ReadOnlySpan<CommandSubmission> submissions)
    {
        foreach (CommandSubmission submission in submissions)
        {
            if (submission.Queue is null)
            {
                continue;
            }

            SemaphoreSubmitInfo waitSemaphoreInfo = new()
            {
                SType = StructureType.SemaphoreSubmitInfo,
                Semaphore = submission.Queue.Vulkan().Semaphore,
                Value = submission.Value,
                StageMask = PipelineStageFlags2.AllCommandsBit
            };

            SubmitInfo2 submitInfo = new()
            {
                SType = StructureType.SubmitInfo2,
                WaitSemaphoreInfoCount = 1,
                PWaitSemaphoreInfos = &waitSemaphoreInfo
            };

            Context.Vk.QueueSubmit2(Queue, 1, &submitInfo, default).Success();
        }
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

    protected override void Destroy()
    {
        base.Destroy();

        Context.Vk.DestroySemaphore(Context.Device, Semaphore, default);
    }
}
