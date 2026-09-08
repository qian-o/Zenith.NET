using Zenith.NET;

namespace Sponza.Models;

internal struct SceneOutput
{
    public Texture HdrColor;

    public Texture IndirectDiffuse;

    public Texture DeviceDepth;

    public Texture EncodedMotion;
}
