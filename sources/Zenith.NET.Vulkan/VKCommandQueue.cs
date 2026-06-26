using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal class VKCommandQueue(VKGraphicsContext context, CommandQueueType type, Queue queue, uint queueFamilyIndex) : CommandQueue(context, type)
{
    public Queue Queue = queue;

    public uint QueueFamilyIndex = queueFamilyIndex;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override ulong GetCompletedValue()
    {
        throw new NotImplementedException();
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        throw new NotImplementedException();
    }

    protected override void SignalImpl(ulong signalValue)
    {
        throw new NotImplementedException();
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        throw new NotImplementedException();
    }

    protected override void WaitImpl(ulong waitValue)
    {
        throw new NotImplementedException();
    }

    protected override void WaitImpl(ReadOnlySpan<CommandSubmission> submissions)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
    }
}
