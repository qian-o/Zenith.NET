namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context), IBindableResource
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract BufferView View { get; }

    public abstract nint SharedPointer { get; }

    public abstract void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes);

    public abstract void Download<T>(Span<T> data, uint offsetInBytes);

    protected void UploadInternal<T>(ReadOnlySpan<T> data, uint offsetInBytes)
    {
        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.Upload(this, offsetInBytes, data);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
