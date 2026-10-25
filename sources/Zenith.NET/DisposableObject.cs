namespace Zenith.NET;

public abstract class DisposableObject : IDisposable
{
    private volatile uint isDisposed;

    ~DisposableObject()
    {
        Dispose();
    }

    public bool IsDisposed => isDisposed is not 0;

    public event EventHandler? Disposing;

    public event EventHandler? Disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
        {
            return;
        }

        Disposing?.Invoke(this, EventArgs.Empty);

        Destroy();

        Disposed?.Invoke(this, EventArgs.Empty);

        GC.SuppressFinalize(this);
    }

    protected abstract void Destroy();
}
