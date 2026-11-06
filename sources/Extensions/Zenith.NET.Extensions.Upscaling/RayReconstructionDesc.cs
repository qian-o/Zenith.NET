namespace Zenith.NET.Extensions.Upscaling;

public struct RayReconstructionDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat DiffuseAlbedoFormat;

    public PixelFormat SpecularAlbedoFormat;

    public PixelFormat NormalsFormat;

    public PixelFormat RoughnessFormat;

    public PixelFormat SpecularHitDistanceFormat;

    public PixelFormat? LinearDepthDeltaFormat;

    public PixelFormat? DirectDiffuseRadianceFormat;

    public PixelFormat? DirectSpecularRadianceFormat;

    public PixelFormat? IndirectDiffuseRadianceFormat;

    public PixelFormat? IndirectSpecularRadianceFormat;

    public PixelFormat? ReactiveMaskFormat;

    public PixelFormat? TransparencyOverlayFormat;

    public PixelFormat OutputFormat;

    public bool IsHdr;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public UpscalingInputFlags Flags;
}
