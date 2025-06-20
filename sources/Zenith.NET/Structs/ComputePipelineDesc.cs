namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader Shader;

    public ResourceLayout[] ResourceLayouts;
}
