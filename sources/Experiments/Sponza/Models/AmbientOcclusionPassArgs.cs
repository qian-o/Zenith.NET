using Zenith.NET;

namespace Sponza.Models;

internal struct AmbientOcclusionPassArgs
{
    public Texture HdrColor;

    public Texture IndirectDiffuse;

    public Texture DeviceDepth;

    public FrameData Frame;

    public float RadiusInMeters;

    public float Strength;
}
