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
        IsDepthClipEnabled = true,
        IsScissorEnabled = false
    };

    public static readonly RasterizerState CullFront = new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullMode.Front,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        DepthBiasSlopeScale = 0.0f,
        IsDepthClipEnabled = true,
        IsScissorEnabled = false
    };

    public static readonly RasterizerState CullBack = new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullMode.Back,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        DepthBiasSlopeScale = 0.0f,
        IsDepthClipEnabled = true,
        IsScissorEnabled = false
    };

    public static readonly RasterizerState CullNone = new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullMode.None,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        DepthBiasSlopeScale = 0.0f,
        IsDepthClipEnabled = true,
        IsScissorEnabled = false
    };

    public static readonly RasterizerState WireframeCullFront = new()
    {
        FillMode = FillMode.Wireframe,
        CullMode = CullMode.Front,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        DepthBiasSlopeScale = 0.0f,
        IsDepthClipEnabled = true,
        IsScissorEnabled = false
    };

    public static readonly RasterizerState WireframeCullBack = new()
    {
        FillMode = FillMode.Wireframe,
        CullMode = CullMode.Back,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        DepthBiasSlopeScale = 0.0f,
        IsDepthClipEnabled = true,
        IsScissorEnabled = false
    };

    public static readonly RasterizerState Wireframe = new()
    {
        FillMode = FillMode.Wireframe,
        CullMode = CullMode.None,
        FrontFace = FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0.0f,
        DepthBiasSlopeScale = 0.0f,
        IsDepthClipEnabled = true,
        IsScissorEnabled = false
    };
}
