namespace Zenith.NET;

public struct ComputePipelineDesc
{
    public Shader Shader { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }
}
