namespace Zenith.NET;

public abstract class Capabilities
{
    public abstract bool IsRayQuerySupported { get; }

    public abstract bool IsRayTracingSupported { get; }
}
