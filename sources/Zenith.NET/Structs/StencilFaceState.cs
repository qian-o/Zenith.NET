namespace Zenith.NET;

public record struct StencilFaceState
{
    public StencilOperation Fail;

    public StencilOperation DepthFail;

    public StencilOperation Pass;

    public CompareFunction Compare;
}
