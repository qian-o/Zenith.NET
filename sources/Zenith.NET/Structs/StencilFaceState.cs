namespace Zenith.NET;

public record struct StencilFaceState
{
    public StencilOp FailOp;

    public StencilOp DepthFailOp;

    public StencilOp PassOp;

    public CompareOp CompareOp;
}
