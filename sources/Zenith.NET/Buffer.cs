namespace Zenith.NET;

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    public abstract ResourceHandle ConstantHandle { get; }

    public abstract ResourceHandle StorageReadOnlyHandle { get; }

    public abstract ResourceHandle StorageReadWriteHandle { get; }

    public abstract nint Map();

    public abstract void Unmap();

    public void Upload(uint offsetInBytes, BufferData data)
    {
        if (desc.Residency is MemoryResidency.CpuWriteOnly)
        {
            nint pointer = Map();

            unsafe
            {
                new ReadOnlySpan<byte>((void*)data.Pointer, (int)data.SizeInBytes).CopyTo(new((void*)(pointer + offsetInBytes), (int)data.SizeInBytes));
            }

            Unmap();
        }
        else
        {
            CommandBuffer commandBuffer = Context.TransferQueue.CommandBuffer();

            commandBuffer.Upload(this, offsetInBytes, data);
            commandBuffer.Submit().Wait();
        }
    }

    public void Download(uint offsetInBytes, BufferData data)
    {
        if (desc.Residency is MemoryResidency.CpuReadOnly)
        {
            nint pointer = Map();

            unsafe
            {
                new ReadOnlySpan<byte>((void*)(pointer + offsetInBytes), (int)data.SizeInBytes).CopyTo(new((void*)data.Pointer, (int)data.SizeInBytes));
            }

            Unmap();
        }
        else
        {
            CommandBuffer commandBuffer = Context.TransferQueue.CommandBuffer();

            commandBuffer.Download(this, offsetInBytes, data);
            commandBuffer.Submit().Wait();
        }
    }
}
