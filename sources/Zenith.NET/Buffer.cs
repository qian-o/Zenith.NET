namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context), IBuffer
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract nint Pointer { get; }

    public void Upload<T>(uint offsetInBytes, ReadOnlySpan<T> data)
    {
        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.UploadBuffer(this, offsetInBytes, data);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
