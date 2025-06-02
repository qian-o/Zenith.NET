namespace Zenith.NET;

public abstract class DisposableObject : IDisposable
{
    private volatile uint isDisposed;

    ~DisposableObject()
    {
        Destroy();
    }

    public bool IsDisposed => isDisposed is not 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
        {
            return;
        }

        Destroy();

        GC.SuppressFinalize(this);
    }

    protected abstract void Destroy();
}
