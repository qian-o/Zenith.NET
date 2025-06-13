using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    public BufferDesc Desc { get; } = desc;

    public abstract nint SharedPointer { get; }

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes = 0)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Data cannot be empty.", nameof(data));
        }

        if ((uint)(data.Length * Unsafe.SizeOf<T>()) + offsetInBytes > Desc.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Data exceeds buffer size.");
        }

        CommandBuffer commandBuffer = Context.Copy!.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.UpdateBuffer(this, data, offsetInBytes);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
