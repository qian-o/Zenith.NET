namespace Zenith.NET;

internal class BufferUploader(GraphicsContext context) : DisposableObject
{
    private const uint MinBufferSize = 1024 * 4;

    private readonly Lock @lock = new();
    private readonly List<Buffer> available = [];
    private readonly Dictionary<CommandBuffer, Buffer[]> used = [];

    public Buffer Buffer(CommandBuffer commandBuffer, uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!used.TryGetValue(commandBuffer, out Buffer[]? buffers))
        {
            used[commandBuffer] = buffers = [];
        }

        Buffer? buffer = null;

        foreach (Buffer item in available)
        {
            if (item.Desc.SizeInBytes >= sizeInBytes)
            {
                buffer = item;

                available.Remove(item);

                break;
            }
        }

        if (buffer is null)
        {
            sizeInBytes = Math.Max(sizeInBytes, MinBufferSize);

            buffer = context.Factory.CreateBuffer(new()
            {
                SizeInBytes = sizeInBytes,
                StrideInBytes = 1,
                Flags = BufferUsageFlags.Dynamic
            });
        }

        used[commandBuffer] = [.. buffers, buffer];

        return buffer;
    }

    public void Release(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (used.TryGetValue(commandBuffer, out Buffer[]? buffers))
        {
            available.AddRange(buffers);

            used.Remove(commandBuffer);
        }
    }

    protected override void Destroy()
    {
        foreach (Buffer buffer in available)
        {
            buffer.Dispose();
        }
        available.Clear();

        foreach (Buffer[] buffers in used.Values)
        {
            foreach (Buffer buffer in buffers)
            {
                buffer.Dispose();
            }
        }
        used.Clear();
    }
}
