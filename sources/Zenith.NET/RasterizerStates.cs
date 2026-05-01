namespace Zenith.NET;

public static class RasterizerStates
{
    public static readonly RasterizerState Default = new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullMode.None,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        DepthBiasSlopeScale = 0.0f,
        DepthClipEnable = true,
        ScissorEnable = false
    };

    public static readonly RasterizerState CullFront = Default with
    {
        CullMode = CullMode.Front
    };

    public static readonly RasterizerState CullBack = Default with
    {
        CullMode = CullMode.Back
    };

    public static readonly RasterizerState CullNone = Default with
    {
        CullMode = CullMode.None
    };

    public static readonly RasterizerState WireframeCullFront = Default with
    {
        FillMode = FillMode.Wireframe,
        CullMode = CullMode.Front
    };

    public static readonly RasterizerState WireframeCullBack = Default with
    {
        FillMode = FillMode.Wireframe,
        CullMode = CullMode.Back
    };

    public static readonly RasterizerState Wireframe = Default with
    {
        FillMode = FillMode.Wireframe,
        CullMode = CullMode.None
    };
}
