namespace Zenith.NET;

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> available = [];
    private readonly Queue<CommandBuffer> execution = [];

    public CommandQueueType Type { get; } = type;

    public CommandBuffer CommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        return available.Count is 0 ? CreateCommandBuffer() : available.Dequeue();
    }

    public void WaitIdle()
    {
        using Lock.Scope _ = @lock.EnterScope();

        WaitIdleImpl();

        foreach (CommandBuffer commandBuffer in execution)
        {
            commandBuffer.Reset();

            available.Enqueue(commandBuffer);
        }

        execution.Clear();
    }

    internal void Submit(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        SubmitImpl(commandBuffer);

        execution.Enqueue(commandBuffer);
    }

    protected abstract CommandBuffer CreateCommandBuffer();

    protected abstract void WaitIdleImpl();

    protected abstract void SubmitImpl(CommandBuffer commandBuffer);

    protected override void Destroy()
    {
        foreach (CommandBuffer commandBuffer in available)
        {
            commandBuffer.Dispose();
        }
        available.Clear();

        foreach (CommandBuffer commandBuffer in execution)
        {
            commandBuffer.Dispose();
        }
        execution.Clear();
    }
}
