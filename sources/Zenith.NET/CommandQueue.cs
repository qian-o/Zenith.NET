namespace Zenith.NET;

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly List<CommandBuffer> available = [];
    private readonly List<CommandBuffer> execution = [];

    public CommandQueueType Type { get; } = type;

    public CommandBuffer CommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        CommandBuffer commandBuffer;

        if (available.Count > 0)
        {
            commandBuffer = available[0];
            commandBuffer.Reset();

            available.RemoveAt(0);
        }
        else
        {
            commandBuffer = CreateCommandBuffer();
        }

        return commandBuffer;
    }

    public void WaitIdle()
    {
        using Lock.Scope _ = @lock.EnterScope();

        WaitIdleImpl();

        available.AddRange(execution);
        execution.Clear();
    }

    internal void Submit(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        SubmitImpl(commandBuffer);

        execution.Add(commandBuffer);
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
