namespace Zenith.NET;

public struct GraphicsShaders
{
    public Shader Vertex { get; set; }

    public Shader? Hull { get; set; }

    public Shader? Domain { get; set; }

    public Shader? Geometry { get; set; }

    public Shader Pixel { get; set; }
}
