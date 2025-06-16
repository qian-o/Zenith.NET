namespace Zenith.NET;

public struct DepthStencilState
{
    public bool DepthEnable { get; set; }

    public bool DepthWriteEnable { get; set; }

    public ComparisonFunc DepthFunc { get; set; }

    public bool StencilEnable { get; set; }

    public byte StencilReadMask { get; set; }

    public byte StencilWriteMask { get; set; }

    public DepthStencilStateOp FrontFace { get; set; }

    public DepthStencilStateOp BackFace { get; set; }
}
