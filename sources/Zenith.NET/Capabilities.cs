namespace Zenith.NET;

public abstract class GraphicsCapabilities(GraphicsContext context)
{
    public GraphicsContext Context => context;

    public abstract string DeviceName { get; }

    public abstract bool RayTracingSupported { get; }

    public abstract bool MeshShadingSupported { get; }
}
