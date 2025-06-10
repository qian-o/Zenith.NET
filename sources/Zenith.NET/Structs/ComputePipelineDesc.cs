namespace Zenith.NET;

public record struct ComputePipelineDesc : IDesc
{
    public ComputePipelineDesc()
    {
        Shader = null!;
        ResourceLayouts = [];
    }

    public Shader Shader { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }

    public readonly bool Validate()
    {
        if (Shader is null)
        {
            return false;
        }

        if (ResourceLayouts is null)
        {
            return false;
        }

        return true;
    }
}
