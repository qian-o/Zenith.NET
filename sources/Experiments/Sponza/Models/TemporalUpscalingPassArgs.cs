using Zenith.NET;

namespace Sponza.Models;

internal struct TemporalUpscalingPassArgs
{
    public Texture HdrColor;

    public Texture DeviceDepth;

    public Texture EncodedMotion;

    public FrameData Frame;
}
