namespace Zenith.NET;

public record struct GraphicsShaders
{
    public Shader Vertex;

    public Shader? Hull;

    public Shader? Domain;

    public Shader? Geometry;

    public Shader Pixel;
}
