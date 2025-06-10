using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct GraphicsPipelineDesc : IDesc
{
    public GraphicsPipelineDesc()
    {
        RenderStates = new();
        Shaders = new();
        InputLayouts = [];
        ResourceLayouts = [];
        PrimitiveTopology = PrimitiveTopology.TriangleList;
        Outputs = new();
    }

    public RenderStatesDesc RenderStates { get; set; }

    public GraphicsShadersDesc Shaders { get; set; }

    public InputLayoutDesc[] InputLayouts { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }

    public PrimitiveTopology PrimitiveTopology { get; set; }

    public OutputDesc Outputs { get; set; }

    public readonly bool Validate()
    {
        if (!RenderStates.Validate())
        {
            return false;
        }

        if (!Shaders.Validate())
        {
            return false;
        }

        if (!Validation.IsAllValidDescs(InputLayouts))
        {
            return false;
        }

        if (ResourceLayouts is null)
        {
            return false;
        }

        if (!Enum.IsDefined(PrimitiveTopology))
        {
            return false;
        }

        if (!Outputs.Validate())
        {
            return false;
        }

        return true;
    }
}
