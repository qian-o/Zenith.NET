namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    public BufferDesc Desc { get; } = desc;

    public abstract nint SharedPointer { get; }

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes = 0)
    {
        CommandBuffer commandBuffer = Context.Copy!.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.UpdateBuffer(this, data, offsetInBytes);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
