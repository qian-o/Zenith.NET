using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct RayTracingPipelineDesc : IDesc
{
    public RayTracingShadersDesc Shaders { get; set; }

    public HitGroupDesc[] HitGroups { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }

    public uint MaxTraceRecursionDepth { get; set; }

    public uint MaxPayloadSizeInBytes { get; set; }

    public uint MaxAttributeSizeInBytes { get; set; }

    public readonly bool Validate()
    {
        if (!Shaders.Validate())
        {
            return false;
        }

        if (!Validation.IsValidDescs(HitGroups))
        {
            return false;
        }

        if (ResourceLayouts is null)
        {
            return false;
        }

        if (MaxTraceRecursionDepth > 31)
        {
            return false;
        }

        if (MaxPayloadSizeInBytes % 4 is not 0)
        {
            return false;
        }

        if (MaxAttributeSizeInBytes % 4 is not 0 || MaxAttributeSizeInBytes > 32)
        {
            return false;
        }

        return true;
    }
}
