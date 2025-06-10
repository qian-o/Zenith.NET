using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    public BufferDesc Desc { get; } = desc;

    public abstract nint SharedPointer { get; }

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes = 0) where T : unmanaged
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Data cannot be empty.", nameof(data));
        }

        if ((uint)(data.Length * Unsafe.SizeOf<T>()) + offsetInBytes > Desc.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Data exceeds buffer size.");
        }

        // TODO: Use the internal Copy queue to upload data and wait for completion.
    }
}
