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

        if (available.FirstOrDefault(item => item.Desc.SizeInBytes >= sizeInBytes) is not Buffer buffer || !available.Remove(buffer))
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

        if (used.Remove(commandBuffer, out Buffer[]? buffers))
        {
            available.AddRange(buffers);
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
