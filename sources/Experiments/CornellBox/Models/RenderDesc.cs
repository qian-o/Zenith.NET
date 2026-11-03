using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Models;

internal struct RenderDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public UpscalerKind Upscaler;

    public UpscalingMode UpscalingMode;

    public FrameGenerationMode FrameGeneration;
}
