namespace Zenith.NET;

public record struct RayTracingPipelineDesc
{
    public Shader RayGeneration;

    public Shader[] Miss;

    public Shader[] AnyHit;

    public Shader[] Intersection;

    public Shader[] ClosestHit;

    public HitGroup[] HitGroups;

    public ResourceLayout[] ResourceLayouts;

    public uint MaxTraceRecursionDepth;

    public uint MaxPayloadSizeInBytes;

    public uint MaxAttributeSizeInBytes;
}
