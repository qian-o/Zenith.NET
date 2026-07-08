namespace Zenith.NET;

public abstract class Timeline(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private readonly Lock @lock = new();

    private ulong nextValue;

    public abstract ulong CompletedValue { get; }

    public TimelineValue Signal()
    {
        using Lock.Scope _ = @lock.EnterScope();

        SignalImpl(queue, ++nextValue);

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

    protected abstract void SignalImpl(CommandQueue queue, ulong value);

    protected abstract void WaitImpl(ulong value);
}
