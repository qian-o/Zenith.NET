namespace Zenith.NET;

public struct StencilFaceState
{
    public StencilOp FailOp;

    public StencilOp DepthFailOp;

    public StencilOp PassOp;

    public CompareOp CompareOp;

    public static StencilFaceState Keep()
    {
        return new()
        {
            FailOp = StencilOp.Keep,
            DepthFailOp = StencilOp.Keep,
            PassOp = StencilOp.Keep,
            CompareOp = CompareOp.Always
        };
    }
}
