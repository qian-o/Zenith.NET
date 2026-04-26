namespace Zenith.NET;

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> available = [];
    private readonly Queue<PendingCommandBuffer> execution = [];

    private ulong value;

    public CommandQueueType Type { get; } = type;

    public CommandBuffer CommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        Recycle();

        CommandBuffer commandBuffer = available.Count is 0 ? CreateCommandBuffer() : available.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    internal CommandSubmission Submit(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits)
    {
        using Lock.Scope _ = @lock.EnterScope();

        Recycle();

        commandBuffer.End();

        SubmitImpl(commandBuffer, waits, ++value);

        execution.Enqueue(new(commandBuffer, value));

        return new(this, value);
    }

    internal void Wait(ulong value)
    {
        if (value is 0)
        {
            return;
        }

        WaitImpl(value);
    }

    protected abstract ulong GetCompletedValue();

    protected abstract CommandBuffer CreateCommandBuffer();

    protected abstract void SubmitImpl(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue);

    protected abstract void WaitImpl(ulong value);

    protected override void Destroy()
    {
        Wait(value);

        while (available.TryDequeue(out CommandBuffer? commandBuffer))
        {
            commandBuffer.Dispose();
        }

        while (execution.TryDequeue(out PendingCommandBuffer pending))
        {
            pending.CommandBuffer.Dispose();
        }
    }

    private void Recycle()
    {
        ulong completed = GetCompletedValue();

        while (execution.TryPeek(out PendingCommandBuffer pending) && pending.Value <= completed)
        {
            execution.Dequeue();

            pending.CommandBuffer.Reset();

            available.Enqueue(pending.CommandBuffer);
        }
    }

    private readonly record struct PendingCommandBuffer(CommandBuffer CommandBuffer, ulong Value);
}
