using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKTimeline : Timeline
{
    public VkSemaphore Semaphore;

    public VKTimeline(VKGraphicsContext context, VKCommandQueue queue) : base(context, queue)
    {
        SemaphoreCreateInfo createInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        createInfo.AddNext(out SemaphoreTypeCreateInfo typeCreateInfo);
        typeCreateInfo.SemaphoreType = SemaphoreType.Timeline;

        context.Vk.CreateSemaphore(context.Device, &createInfo, default, out Semaphore).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public new VKCommandQueue Queue => (VKCommandQueue)base.Queue;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override ulong GetCompletedValue()
    {
        Context.Vk.GetSemaphoreCounterValue(Context.Device, Semaphore, out ulong value).Success();

        return value;
    }

    protected override void SignalImpl(ulong value)
    {
        SemaphoreSubmitInfo signalSemaphoreInfo = new()
        {
            SType = StructureType.SemaphoreSubmitInfo,
            Semaphore = Semaphore,
            Value = value,
            StageMask = PipelineStageFlags2.AllCommandsBit
        };

        SubmitInfo2 submitInfo = new()
        {
            SType = StructureType.SubmitInfo2,
            SignalSemaphoreInfoCount = 1,
            PSignalSemaphoreInfos = &signalSemaphoreInfo
        };

        Context.Vk.QueueSubmit2(Queue.Queue, 1, &submitInfo, default).Success();
    }

    protected override void WaitImpl(ulong value)
    {
        fixed (VkSemaphore* pSemaphores = &Semaphore)
        {
            SemaphoreWaitInfo waitInfo = new()
            {
                SType = StructureType.SemaphoreWaitInfo,
                SemaphoreCount = 1,
                PSemaphores = pSemaphores,
                PValues = &value
            };

            Context.Vk.WaitSemaphores(Context.Device, &waitInfo, ulong.MaxValue).Success();
        }
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Semaphore,
            ObjectHandle = Semaphore.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroySemaphore(Context.Device, Semaphore, default);
    }
}
