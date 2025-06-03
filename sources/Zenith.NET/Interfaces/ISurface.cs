using System.Numerics;

namespace Zenith.NET;

public interface ISurface
{
    SurfaceType SurfaceType { get; }

    nint[] Handles { get; }

    Vector2 Size { get; }
}