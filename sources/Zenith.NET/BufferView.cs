namespace Zenith.NET;

public abstract class BufferView(GraphicsContext context, BufferViewDesc desc) : GraphicsResource(context), IBuffer
{
    private BufferViewDesc desc = desc;

    public ref readonly BufferViewDesc Desc => ref desc;

    public nint Pointer { get; } = desc.Buffer.Pointer is not 0 ? (nint)(desc.Buffer.Pointer + desc.OffsetInBytes) : 0;

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Buffer subresource upload validation is not implemented yet.");
        }

        Desc.Buffer.Upload(data, Desc.OffsetInBytes + offsetInBytes);
    }
}
