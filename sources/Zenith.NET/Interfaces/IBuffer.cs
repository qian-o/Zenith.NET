namespace Zenith.NET;

public interface IBuffer
{
    nint Pointer { get; }

    void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes);
}