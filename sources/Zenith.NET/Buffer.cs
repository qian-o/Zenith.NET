namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context), IBindableResource
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract MappedMemory Map();

    public abstract void Unmap();

    public void Upload<T>(ReadOnlySpan<T> data, uint offsetInBytes) where T : unmanaged
    {
        if (desc.Flags.HasFlag(BufferUsageFlags.Dynamic))
        {
            MappedMemory mappedMemory = Map();

            unsafe
            {
                data.CopyTo(new((void*)(mappedMemory.Pointer + offsetInBytes), data.Length));
            }

            Unmap();
        }
        else
        {
            CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

            commandBuffer.Begin();
            commandBuffer.Upload(this, offsetInBytes, data);
            commandBuffer.End();
            commandBuffer.Submit();

            Context.Copy.WaitIdle();
        }
    }
}
