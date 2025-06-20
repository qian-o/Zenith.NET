namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    public BufferDesc Desc { get; } = desc;

    public abstract nint SharedPointer { get; }

    public abstract void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes = 0);
}
