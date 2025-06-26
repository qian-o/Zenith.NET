namespace Zenith.NET;

public abstract class BufferSubresource(GraphicsContext context, BufferSubresourceDesc desc) : GraphicsResource(context), IBindableResource, IBufferResource
{
    private BufferSubresourceDesc desc = desc;

    public ref readonly BufferSubresourceDesc Desc => ref desc;

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
