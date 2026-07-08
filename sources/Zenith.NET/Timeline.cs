namespace Zenith.NET;

public abstract class Timeline(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private readonly Lock @lock = new();

    private ulong nextValue;

    public CommandQueue Queue { get; } = queue;

    public abstract ulong CompletedValue { get; }

    public TimelineValue Signal()
    {
        using Lock.Scope _ = @lock.EnterScope();

        SignalImpl(++nextValue);

        return new(this, nextValue);
    }

    public void Wait(ulong value)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (CompletedValue < value)
        {
            WaitImpl(value);
        }
    }

    protected abstract void SignalImpl(ulong value);

    protected abstract void WaitImpl(ulong value);
}
