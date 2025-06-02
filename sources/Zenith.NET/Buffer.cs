using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    public BufferDesc Desc { get; } = desc;

    /// <summary>
    /// The CPU and GPU share access to the resource, allocated in system memory.
    /// Only valid if the <see cref="BufferUsageFlags.Dynamic"/> flag is set.
    /// </summary>
    public abstract nint SharedPointer { get; }

    /// <summary>
    /// Uploads data to the buffer immediately, blocking until the operation is complete.
    /// </summary>
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
