namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderStatesDesc RenderStates { get; set; }

    public GraphicsShadersDesc Shaders { get; set; }

    public InputLayoutDesc[] InputLayouts { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }

    public PrimitiveTopology PrimitiveTopology { get; set; }

    public OutputDesc Outputs { get; set; }
}
