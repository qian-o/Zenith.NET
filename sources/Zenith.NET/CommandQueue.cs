namespace Zenith.NET;

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> commandBuffers = [];
    private readonly Queue<Submitted> submitteds = [];

    private ulong value;

    public CommandQueueType Type { get; } = type;

    public CommandBuffer CommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        Recycle();

        CommandBuffer commandBuffer = commandBuffers.Count is 0 ? CreateCommandBuffer() : commandBuffers.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    internal CommandSubmission Submit(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits)
    {
        using Lock.Scope _ = @lock.EnterScope();

        commandBuffer.End();

        Recycle();

        SubmitImpl(commandBuffer, waits, ++value);

        submitteds.Enqueue(new(commandBuffer, value));

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

        while (commandBuffers.TryDequeue(out CommandBuffer? commandBuffer))
        {
            commandBuffer.Dispose();
        }

        while (submitteds.TryDequeue(out Submitted submitted))
        {
            submitted.CommandBuffer.Dispose();
        }
    }

    private void Recycle()
    {
        ulong completed = GetCompletedValue();

        while (submitteds.TryPeek(out Submitted submitted) && submitted.Value <= completed)
        {
            submitteds.Dequeue();

            submitted.CommandBuffer.Reset();

            commandBuffers.Enqueue(submitted.CommandBuffer);
        }
    }

    private readonly record struct Submitted(CommandBuffer CommandBuffer, ulong Value);
}
