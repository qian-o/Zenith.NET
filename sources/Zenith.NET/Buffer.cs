namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract nint SharedPointer { get; }

    public abstract void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes = 0);
}
