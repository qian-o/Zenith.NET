namespace Zenith.NET;

public abstract class Capabilities
{
    public abstract string DeviceName { get; }

    public abstract bool RayTracing { get; }

    public abstract bool MeshShading { get; }
}
