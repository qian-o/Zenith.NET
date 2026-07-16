namespace Zenith.NET;

public abstract class Timeline(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private readonly Lock @lock = new();

    private ulong nextValue;

    public CommandQueue Queue { get; } = queue;

    public TimelineValue Signal()
    {
        using Lock.Scope _ = @lock.EnterScope();

        SignalImpl(++nextValue);

        return new(this, nextValue);
    }

    public void Wait(ulong value)
    {
        using (Lock.Scope _ = @lock.EnterScope())
        {
            if (value > GetCompletedValue())
            {
                WaitImpl(value);
            }
        }

        Queue.Recycle(value);
    }

    protected abstract ulong GetCompletedValue();

    protected abstract void SignalImpl(ulong value);

    protected abstract void WaitImpl(ulong value);
}
