namespace Zenith.NET;

public struct RayTracingPipelineDesc
{
    public RayTracingShaders Shaders { get; set; }

    public HitGroup[] HitGroups { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }

    public uint MaxTraceRecursionDepth { get; set; }

    public uint MaxPayloadSizeInBytes { get; set; }

    public uint MaxAttributeSizeInBytes { get; set; }
}
