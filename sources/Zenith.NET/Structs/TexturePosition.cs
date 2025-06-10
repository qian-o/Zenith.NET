namespace Zenith.NET;

public record struct TexturePosition
{
    public TexturePosition()
    {
        X = 0;
        Y = 0;
        Z = 0;
        FaceIndex = 0;
        ArrayLayer = 0;
        MipLevel = 0;
    }

    public uint X { get; set; }

    public uint Y { get; set; }

    public uint Z { get; set; }

    public uint FaceIndex { get; set; }

    public uint ArrayLayer { get; set; }

    public uint MipLevel { get; set; }
}
