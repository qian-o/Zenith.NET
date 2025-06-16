namespace Zenith.NET;

public record struct ResourceSetDesc
{
    public ResourceLayout Layout { get; set; }

    public GraphicsResource[] Resources { get; set; }
}
