namespace Zenith.NET;

public record struct DepthStencilState
{
    public bool IsDepthEnabled;

    public bool IsDepthWriteEnabled;

    public CompareFunction DepthCompareFunction;

    public bool IsStencilEnabled;

    public byte StencilReadMask;

    public byte StencilWriteMask;

    public StencilFaceState FrontFace;

    public StencilFaceState BackFace;
}
