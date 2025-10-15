namespace Zenith.NET;

public interface IDisposableObject : IDisposable
{
    bool IsDisposed { get; }
}
