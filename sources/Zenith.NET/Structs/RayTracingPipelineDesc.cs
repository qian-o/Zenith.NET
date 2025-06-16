namespace Zenith.NET;

public record struct RayTracingPipelineDesc
{
    public RayTracingShadersDesc Shaders { get; set; }

    public HitGroupDesc[] HitGroups { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }

    public uint MaxTraceRecursionDepth { get; set; }

    public uint MaxPayloadSizeInBytes { get; set; }

    public uint MaxAttributeSizeInBytes { get; set; }
}
