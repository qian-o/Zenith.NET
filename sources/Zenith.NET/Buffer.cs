namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context), IBuffer
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract nint Pointer { get; }

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes)
    {
        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.UploadBuffer(data, this, offsetInBytes);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
