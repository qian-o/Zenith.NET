namespace Zenith.NET;

public interface IBuffer : IBindableResource
{
    nint Pointer { get; }

    void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes);
}