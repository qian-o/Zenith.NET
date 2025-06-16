namespace Zenith.NET;

public struct GraphicsPipelineDesc
{
    public RenderStates RenderStates { get; set; }

    public GraphicsShaders Shaders { get; set; }

    public InputLayout[] InputLayouts { get; set; }

    public ResourceLayout[] ResourceLayouts { get; set; }

    public PrimitiveTopology PrimitiveTopology { get; set; }

    public Output Outputs { get; set; }
}
