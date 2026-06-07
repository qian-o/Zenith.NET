namespace Zenith.NET;

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> commandBuffers = [];
    private readonly Queue<Submitted> submitteds = [];

    private ulong value;

    public CommandQueueType Type { get; } = type;

    public CommandBuffer AcquireCommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        CommandBuffer commandBuffer = commandBuffers.Count is 0 ? CreateCommandBuffer() : commandBuffers.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    internal CommandSubmission Submit(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits)
    {
        using Lock.Scope _ = @lock.EnterScope();

        commandBuffer.End();

        SubmitImpl(commandBuffer, waits, ++value);

        submitteds.Enqueue(new(commandBuffer, value));

        return new(this, value);
    }

    internal void Wait(ulong value)
    {
        using Lock.Scope _ = @lock.EnterScope();

        ulong completed = GetCompletedValue();

        if (completed < value)
        {
            WaitImpl(value);

            completed = GetCompletedValue();
        }

        while (submitteds.TryPeek(out Submitted submitted) && submitted.Value <= completed)
        {
            submitteds.Dequeue();

            submitted.CommandBuffer.Reset();

            commandBuffers.Enqueue(submitted.CommandBuffer);
        }
    }

    internal void WaitIdle()
    {
        Wait(value);
    }

    protected abstract ulong GetCompletedValue();

    protected abstract CommandBuffer CreateCommandBuffer();

    protected abstract void SubmitImpl(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue);

    protected abstract void WaitImpl(ulong waitValue);

    protected override void Destroy()
    {
        WaitIdle();

        while (commandBuffers.TryDequeue(out CommandBuffer? commandBuffer))
        {
            commandBuffer.Dispose();
        }

        while (submitteds.TryDequeue(out Submitted submitted))
        {
            submitted.CommandBuffer.Dispose();
        }
    }

    private readonly struct Submitted(CommandBuffer commandBuffer, ulong value)
    {
        public readonly CommandBuffer CommandBuffer = commandBuffer;

        public readonly ulong Value = value;
    }
}
