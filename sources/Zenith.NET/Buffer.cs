namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context), IBindableResource
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract BufferView View { get; }

    public abstract nint SharedPointer { get; }

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes)
    {
        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.UploadBuffer(this, offsetInBytes, data);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }

    public abstract ReadOnlySpan<T> Download<T>(int length, uint offsetInBytes);
}
