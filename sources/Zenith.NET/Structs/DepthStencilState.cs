namespace Zenith.NET;

public record struct DepthStencilState
{
    public bool DepthEnable;

    public bool DepthWriteEnable;

    public CompareFunction DepthCompare;

    public bool StencilEnable;

    public byte StencilReadMask;

    public byte StencilWriteMask;

    public StencilFaceState FrontFace;

    public StencilFaceState BackFace;
}
