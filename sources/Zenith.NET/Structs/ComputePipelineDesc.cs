namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader ComputeShader;

    public ResourceLayout[] ResourceLayouts;
}
