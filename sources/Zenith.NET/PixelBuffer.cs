namespace Zenith.NET;

public abstract class PixelBuffer
{
    public abstract nint NativePointer { get; }

    public abstract uint SizeInBytes { get; }

    public abstract void Lock();

    public abstract void Unlock();
}
