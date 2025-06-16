namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader Shader { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }
}
