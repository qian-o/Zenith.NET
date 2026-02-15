namespace Zenith.NET;

[Flags]
public enum ShaderStageFlags
{
    None = 0,

    Vertex = 1 << 0,

    Pixel = 1 << 1,

    Compute = 1 << 2,

    Amplification = 1 << 3,

    Mesh = 1 << 4
}