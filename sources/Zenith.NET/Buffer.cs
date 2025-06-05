using System.Runtime.CompilerServices;

namespace Zenith.NET;

/// <summary>
/// Represents a GPU buffer resource, providing methods for data upload and shared memory access.
/// </summary>
public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the buffer description.
    /// </summary>
    public BufferDesc Desc { get; } = desc;

    /// <summary>
    /// Gets a pointer to shared system memory for CPU/GPU access.
    /// Only valid if the <see cref="BufferUsageFlags.Dynamic"/> flag is set.
    /// </summary>
    public abstract nint SharedPointer { get; }

    /// <summary>
    /// Uploads data to the buffer immediately, blocking until the operation is complete.
    /// </summary>
    /// <typeparam name="T">The type of data to upload. Must be unmanaged.</typeparam>
    /// <param name="data">The data to upload.</param>
    /// <param name="offsetInBytes">The offset in bytes at which to start writing data.</param>
    /// <exception cref="ArgumentException">Thrown if data is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if data exceeds buffer size.</exception>
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
