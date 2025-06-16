namespace Zenith.NET;

public record struct DepthStencilStateDesc
{
    public bool DepthEnable { get; set; }

    public bool DepthWriteEnable { get; set; }

    public ComparisonFunc DepthFunc { get; set; }

    public bool StencilEnable { get; set; }

    public byte StencilReadMask { get; set; }

    public byte StencilWriteMask { get; set; }

    public DepthStencilOpStateDesc FrontFace { get; set; }

    public DepthStencilOpStateDesc BackFace { get; set; }
}
