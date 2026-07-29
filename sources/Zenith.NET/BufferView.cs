namespace Zenith.NET;

public abstract class BufferView(GraphicsContext context, BufferViewDesc desc) : GraphicsResource(context)
{
    private BufferViewDesc desc = desc;

    public ref readonly BufferViewDesc Desc => ref desc;

    public abstract ResourceHandle ConstantHandle { get; }

    public abstract ResourceHandle StorageReadOnlyHandle { get; }

    public abstract ResourceHandle StorageReadWriteHandle { get; }
}
