namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract MappedMemory Map();

    public abstract void Unmap();

    public void Upload(uint offsetInBytes, BufferData data)
    {
        if (desc.Access is BufferAccess.CpuWriteOnly)
        {
            MappedMemory mappedMemory = Map();

            unsafe
            {
                new ReadOnlySpan<byte>((void*)data.Pointer, (int)data.SizeInBytes).CopyTo(new((void*)(mappedMemory.Pointer + offsetInBytes), (int)data.SizeInBytes));
            }

            Unmap();
        }
        else
        {
            CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

            commandBuffer.Upload(this, offsetInBytes, data);
            commandBuffer.Submit().Wait();
        }
    }

    public void Download(uint offsetInBytes, BufferData data)
    {
        if (desc.Access is BufferAccess.CpuReadOnly)
        {
            MappedMemory mappedMemory = Map();

            unsafe
            {
                new ReadOnlySpan<byte>((void*)(mappedMemory.Pointer + offsetInBytes), (int)data.SizeInBytes).CopyTo(new((void*)data.Pointer, (int)data.SizeInBytes));
            }

            Unmap();
        }
        else
        {
            CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

            commandBuffer.Download(this, offsetInBytes, data);
            commandBuffer.Submit().Wait();
        }
    }
}
