namespace Zenith.NET;

public struct RasterizerState
{
    public FillMode FillMode;

    public CullMode CullMode;

    public FrontFace FrontFace;

    public int DepthBias;

    public float DepthBiasClamp;

    public float DepthBiasSlopeScale;

    public bool IsDepthClipEnabled;

    public bool IsScissorEnabled;

    public static RasterizerState CullFront()
    {
        return new()
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
    }

    public static RasterizerState CullBack()
    {
        return new()
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
    }

    public static RasterizerState CullNone()
    {
        return new()
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
    }

    public static RasterizerState Wireframe()
    {
        return new()
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

    public static RasterizerState WireframeCullFront()
    {
        return new()
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
    }

    public static RasterizerState WireframeCullBack()
    {
        return new()
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
    }
}
