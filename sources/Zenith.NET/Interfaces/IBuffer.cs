namespace Zenith.NET;

public interface IBuffer : IBindableResource, IDisposableObject
{
    nint Pointer { get; }

    void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes);
}