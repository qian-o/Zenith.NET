using Zenith.NET;

namespace Sponza.Models;

internal struct UpscalingPassArgs
{
    public Texture Input;

    public Texture Target;

    public TextureLayout TargetLayout;
}
