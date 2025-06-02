namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject
{
    public abstract string Device { get; }

    public abstract Backend Backend { get; }

    public abstract Capabilities Capabilities { get; }

    protected override void Destroy()
    {
    }
}
