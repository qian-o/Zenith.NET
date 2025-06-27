namespace Zenith.NET;

public abstract class BufferView(GraphicsContext context, BufferViewDesc desc) : GraphicsResource(context), IBindableResource, IBuffer
{
    private BufferViewDesc desc = desc;

    public ref readonly BufferViewDesc Desc => ref desc;

    public abstract nint Pointer { get; }

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Buffer subresource upload validation is not implemented yet.");
        }

        Desc.Buffer.Upload(data, Desc.OffsetInBytes + offsetInBytes);
    }
}
