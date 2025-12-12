namespace Zenith.NET;

public abstract class Capabilities
{
    public abstract string DeviceName { get; }

    public abstract bool RayTracingSupported { get; }

    public abstract bool MeshShaderSupported { get; }
}
