namespace Zenith.NET;

public record struct DepthStencilStateDesc : IDesc
{
    public bool DepthEnable { get; set; }

    public bool DepthWriteEnable { get; set; }

    public ComparisonFunc DepthFunc { get; set; }

    public bool StencilEnable { get; set; }

    public byte StencilReadMask { get; set; }

    public byte StencilWriteMask { get; set; }

    public DepthStencilOpStateDesc FrontFace { get; set; }

    public DepthStencilOpStateDesc BackFace { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(DepthFunc))
        {
            return false;
        }

        if (!FrontFace.Validate())
        {
            return false;
        }

        if (!BackFace.Validate())
        {
            return false;
        }

        return true;
    }
}
