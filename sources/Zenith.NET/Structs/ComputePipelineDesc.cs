namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader Compute;

    public ResourceBinding[] Bindings;
}
