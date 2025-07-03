namespace Zenith.NET;

public interface IBuffer : IBindableResource
{
    nint Pointer { get; }

    void Upload<T>(uint offsetInBytes, ReadOnlySpan<T> data);
}