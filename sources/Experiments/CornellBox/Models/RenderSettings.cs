using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Models;

internal struct RenderSettings
{
    public UpscalingMode? UpscalingMode;

    public bool IsPaused;

    public bool IsRayReconstructionEnabled;
}
