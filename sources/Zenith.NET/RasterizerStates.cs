namespace Zenith.NET;

public static class RasterizerStates
{
    public static readonly RasterizerState Default = new()
    {
        CullMode = CullMode.None,
        FillMode = FillMode.Solid,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        SlopeScaledDepthBias = 0.0f,
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
        CullMode = CullMode.Front,
        FillMode = FillMode.Wireframe
    };

    public static readonly RasterizerState WireframeCullBack = Default with
    {
        CullMode = CullMode.Back,
        FillMode = FillMode.Wireframe
    };

    public static readonly RasterizerState Wireframe = Default with
    {
        CullMode = CullMode.None,
        FillMode = FillMode.Wireframe
    };
}
