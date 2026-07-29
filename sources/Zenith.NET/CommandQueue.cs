namespace Zenith.NET;

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> commandBuffers = [];
    private readonly Queue<Submitted> submitteds = [];

    public CommandQueueType Type { get; } = type;

    public abstract Timeline Timeline { get; }

    public CommandBuffer CommandBuffer()
    {
        Poll();

        using Lock.Scope _ = @lock.EnterScope();

        CommandBuffer commandBuffer = commandBuffers.Count is 0 ? CreateCommandBuffer() : commandBuffers.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    public double GetElapsedNanoseconds(ulong startTimestamp, ulong endTimestamp)
    {
        double timestampPeriod = GetTimestampPeriod(out uint validBits);

        ulong elapsedTicks = unchecked(endTimestamp - startTimestamp);

        if (validBits is < 64)
        {
            elapsedTicks &= (1UL << (int)validBits) - 1;
        }

        return elapsedTicks * timestampPeriod;
    }

    internal TimelineValue Submit(ReadOnlySpan<TimelineValue> waits, CommandBuffer commandBuffer)
    {
        Poll();

        using Lock.Scope _ = @lock.EnterScope();

        commandBuffer.End();

        SubmitImpl(waits, commandBuffer);

        TimelineValue timelineValue = Timeline.Signal();

        submitteds.Enqueue(new(commandBuffer, timelineValue));

        return timelineValue;
    }

    internal void Poll()
    {
        using Lock.Scope _ = @lock.EnterScope();

        while (submitteds.TryPeek(out Submitted submitted) && submitted.TimelineValue.IsCompleted)
        {
            submitteds.Dequeue();

            submitted.CommandBuffer.Reset();

            commandBuffers.Enqueue(submitted.CommandBuffer);
        }
    }

    protected abstract CommandBuffer CreateCommandBuffer();

    protected abstract double GetTimestampPeriod(out uint validBits);

    protected abstract void SubmitImpl(ReadOnlySpan<TimelineValue> waits, CommandBuffer commandBuffer);

    protected override void Destroy()
    {
        Timeline.Dispose();

        while (commandBuffers.TryDequeue(out CommandBuffer? commandBuffer))
        {
            commandBuffer.Dispose();
        }

        while (submitteds.TryDequeue(out Submitted submitted))
        {
            submitted.CommandBuffer.Dispose();
        }
    }

    private readonly struct Submitted(CommandBuffer commandBuffer, TimelineValue timelineValue)
    {
        public readonly CommandBuffer CommandBuffer = commandBuffer;

        public readonly TimelineValue TimelineValue = timelineValue;
    }
}
