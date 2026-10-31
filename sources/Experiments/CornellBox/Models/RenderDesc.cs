using Zenith.NET.Extensions.Reconstruction;

namespace CornellBox.Models;

internal struct RenderDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public UpscalerKind Upscaler;

    public UpscalerMode UpscalerMode;

    public FrameGenerationMode FrameGeneration;
}
