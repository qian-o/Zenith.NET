namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSBinding
{
    internal readonly Texture? Texture;

    internal DLSSBinding(Texture texture)
    {
        Texture = texture;
    }
}
