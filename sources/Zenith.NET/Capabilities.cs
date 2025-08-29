namespace Zenith.NET;

public abstract class Capabilities
{
    public abstract string DeviceName { get; }

    public abstract Version ApiVersion { get; }

    public abstract Version DriverVersion { get; }

    public abstract bool SupportsRayTracing { get; }
}
