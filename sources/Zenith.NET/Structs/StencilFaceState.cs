namespace Zenith.NET;

public record struct StencilFaceState
{
    public StencilOperation FailOperation;

    public StencilOperation DepthFailOperation;

    public StencilOperation PassOperation;

    public CompareFunction CompareFunction;
}
