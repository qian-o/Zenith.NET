using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Models;

internal struct RenderSettings
{
    public UpscalingQuality? UpscalingQuality;

    public bool IsPaused;

    public bool IsRayReconstructionEnabled;
}
