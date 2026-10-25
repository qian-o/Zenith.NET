namespace CornellBox.Models;

internal struct RenderDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DenoisingUpscalingMode DenoisingUpscaling;

    public FrameGenerationMode FrameGeneration;
}
