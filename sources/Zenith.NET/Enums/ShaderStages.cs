namespace Zenith.NET;

[Flags]
public enum ShaderStages
{
    None = 0,

    Vertex = 1 << 0,

    Fragment = 1 << 1,

    Compute = 1 << 2,

    Task = 1 << 3,

    Mesh = 1 << 4
}