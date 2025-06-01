namespace Zenith.NET;

public interface ISurface
{
    SurfaceType SurfaceType { get; }

    nint[] Handles { get; }

    uint Width { get; }

    uint Height { get; }
}