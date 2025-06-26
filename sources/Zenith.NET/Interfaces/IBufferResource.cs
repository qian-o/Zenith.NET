namespace Zenith.NET;

public interface IBufferResource
{
    nint Pointer { get; }

    void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes);
}