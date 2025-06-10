namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject
{
    public abstract string Device { get; }

    public abstract Backend Backend { get; }

    public abstract Capabilities Capabilities { get; }

    public abstract ResourceFactory Factory { get; }

    internal BufferUploader? Uploader { get; private set; }

    internal CommandQueue? Copy { get; private set; }

    public void CreateDevice(bool useDebugLayer = false)
    {
        CreateDeviceImpl(useDebugLayer);

        Uploader = new(this);
        Copy = Factory.CreateCommandQueue(CommandQueueType.Copy);
    }

    protected override void Destroy()
    {
        Uploader?.Dispose();
        Uploader = null;

        Copy?.Dispose();
        Copy = null;
    }

    protected abstract void CreateDeviceImpl(bool useDebugLayer);
}
