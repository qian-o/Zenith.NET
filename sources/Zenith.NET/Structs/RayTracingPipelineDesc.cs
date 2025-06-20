namespace Zenith.NET;

public record struct RayTracingPipelineDesc
{
    public RayTracingShaders Shaders;

    public HitGroup[] HitGroups;

    public ResourceLayout[] ResourceLayouts;

    public uint MaxTraceRecursionDepth;

    public uint MaxPayloadSizeInBytes;

    public uint MaxAttributeSizeInBytes;
}
