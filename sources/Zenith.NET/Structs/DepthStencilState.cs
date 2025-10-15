namespace Zenith.NET;

public record struct DepthStencilState
{
    public bool DepthEnable;

    public bool DepthWriteEnable;

    public ComparisonFunc DepthFunc;

    public bool StencilEnable;

    public byte StencilReadMask;

    public byte StencilWriteMask;

    public DepthStencilStateOp FrontFace;

    public DepthStencilStateOp BackFace;
}
