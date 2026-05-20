namespace Zenith.NET;

public abstract class BufferView(GraphicsContext context, BufferViewDesc desc) : GraphicsResource(context)
{
    private BufferViewDesc desc = desc;

    public ref readonly BufferViewDesc Desc => ref desc;

    public ResourceHandle UniformHandle { get; }

    public ResourceHandle StorageReadOnlyHandle { get; }

    public ResourceHandle StorageReadWriteHandle { get; }
}
