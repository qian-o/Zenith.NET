namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject
{
    protected GraphicsContext(Backend backend, bool useDebugLayer)
    {
        Backend = backend;
        UseDebugLayer = useDebugLayer;

        Initialize(out Driver driver,
                   out ResourceFactory factory,
                   out CommandQueue direct,
                   out CommandQueue compute,
                   out CommandQueue copy);

        Driver = driver;
        Factory = factory;
        Direct = direct;
        Compute = compute;
        Copy = copy;
        Uploader = new(this);
    }

    public Backend Backend { get; }

    public bool UseDebugLayer { get; }

    public Driver Driver { get; }

    public ResourceFactory Factory { get; }

    public CommandQueue Direct { get; }

    public CommandQueue Compute { get; }

    public CommandQueue Copy { get; }

    internal BufferUploader Uploader { get; }

    protected override void Destroy()
    {
        Direct.Dispose();
        Compute.Dispose();
        Copy.Dispose();

        Uploader.Dispose();
    }

    protected abstract void Initialize(out Driver driver,
                                       out ResourceFactory factory,
                                       out CommandQueue direct,
                                       out CommandQueue compute,
                                       out CommandQueue copy);
}
