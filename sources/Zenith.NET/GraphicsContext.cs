namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject
{
    protected GraphicsContext(bool useDebugLayer)
    {
        CreateDevice(useDebugLayer);

        Uploader = new(this);
        Copy = Factory.CreateCommandQueue(CommandQueueType.Copy);
    }

    public abstract Backend Backend { get; }

    public abstract Driver Driver { get; }

    public abstract ResourceFactory Factory { get; }

    internal BufferUploader Uploader { get; }

    internal CommandQueue Copy { get; }

    protected override void Destroy()
    {
        Uploader.Dispose();
        Copy.Dispose();
    }

    protected abstract void CreateDevice(bool useDebugLayer);
}
