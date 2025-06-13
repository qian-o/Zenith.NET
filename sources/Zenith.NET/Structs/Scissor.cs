namespace Zenith.NET;

public record struct Scissor
{
    public Scissor()
    {
        X = 0;
        Y = 0;
        Width = 0;
        Height = 0;
    }

    public int X { get; set; }

    public int Y { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }
}
