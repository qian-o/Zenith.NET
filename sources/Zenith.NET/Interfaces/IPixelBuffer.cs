namespace Zenith.NET;

public interface IPixelBuffer
{
    nint NativePointer { get; }

    uint SizeInBytes { get; }

    void Lock();

    void Unlock();
}
