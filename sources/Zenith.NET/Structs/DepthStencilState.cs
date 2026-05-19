namespace Zenith.NET;

public record struct DepthStencilState
{
    public bool IsDepthEnabled;

    public bool IsDepthWriteEnabled;

    public CompareOp DepthCompareOp;

    public bool IsStencilEnabled;

    public byte StencilReadMask;

    public byte StencilWriteMask;

    public StencilFaceState FrontFace;

    public StencilFaceState BackFace;
}
