using Zenith.NET;

namespace Sponza.Models;

internal struct ToneMappingPassArgs
{
    public Texture HdrColor;

    public float Exposure;

    public Texture Target;

    public TextureLayout TargetLayout;
}
