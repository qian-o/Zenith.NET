namespace Zenith.NET;

/// <summary>
/// Provides a base class for all pipeline objects, including graphics, compute, and ray tracing pipelines.
/// Pipelines encapsulate the configuration and state required for executing GPU workloads.
/// </summary>
public abstract class Pipeline(GraphicsContext context) : GraphicsResource(context);