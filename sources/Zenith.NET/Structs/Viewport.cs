namespace Zenith.NET;

public record struct Viewport
{
    public Viewport()
    {
        X = 0;
        Y = 0;
        Width = 0;
        Height = 0;
        MinDepth = 0;
        MaxDepth = 1;
    }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public float MinDepth { get; set; }

    public float MaxDepth { get; set; }
}
