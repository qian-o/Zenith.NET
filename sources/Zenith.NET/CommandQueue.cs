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

        CommandBuffer commandBuffer = commandBuffers.Count is 0 ? CreateCommandBuffer() : commandBuffers.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    internal CommandSubmission Signal()
    {
        using Lock.Scope _ = @lock.EnterScope();

        SignalImpl(++value);

        return new(this, value);
    }

    internal void Submit(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        commandBuffer.End();

        SubmitImpl(commandBuffer);

        submitteds.Enqueue(new(commandBuffer, value));
    }

    internal void Wait(ulong value)
    {
        using Lock.Scope _ = @lock.EnterScope();

        ulong completedValue = GetCompletedValue();

        if (completedValue < value)
        {
            WaitImpl(value);

            completedValue = GetCompletedValue();
        }

        while (submitteds.TryPeek(out Submitted submitted) && submitted.Value <= completedValue)
        {
            submitteds.Dequeue();

            submitted.CommandBuffer.Reset();

            commandBuffers.Enqueue(submitted.CommandBuffer);
        }
    }

    internal void InsertWaits(ReadOnlySpan<CommandSubmission> submissions)
    {
        using Lock.Scope _ = @lock.EnterScope();

        InsertWaitsImpl(submissions);
    }

    protected abstract ulong GetCompletedValue();

    protected abstract CommandBuffer CreateCommandBuffer();

    protected abstract void SignalImpl(ulong signalValue);

    protected abstract void SubmitImpl(CommandBuffer commandBuffer);

    protected abstract void WaitImpl(ulong waitValue);

    protected abstract void InsertWaitsImpl(ReadOnlySpan<CommandSubmission> submissions);

    protected override void Destroy()
    {
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
