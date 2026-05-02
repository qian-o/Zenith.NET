namespace Zenith.NET;

public abstract class Capabilities
{
    public abstract string DeviceName { get; }

    public abstract bool SupportsRayTracing { get; }

    public abstract bool SupportsMeshShading { get; }
}
