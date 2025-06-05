namespace Zenith.NET;

/// <summary>
/// Provides a base class for objects that require deterministic and finalizer-based resource cleanup.
/// </summary>
public abstract class DisposableObject : IDisposable
{
    private volatile uint isDisposed;

    /// <summary>
    /// Finalizer to ensure resources are released if Dispose is not called.
    /// </summary>
    ~DisposableObject()
    {
        Destroy();
    }

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    public bool IsDisposed => isDisposed is not 0;

    /// <summary>
    /// Disposes the object and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
        {
            return;
        }

        Destroy();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases unmanaged and managed resources.
    /// </summary>
    protected abstract void Destroy();
}
