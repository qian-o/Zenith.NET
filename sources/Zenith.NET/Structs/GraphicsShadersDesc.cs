namespace Zenith.NET;

public record struct GraphicsShadersDesc : IDesc
{
    public GraphicsShadersDesc()
    {
        Vertex = null!;
        Hull = null;
        Domain = null;
        Geometry = null;
        Pixel = null!;
    }

    public Shader Vertex { get; set; }

    public Shader? Hull { get; set; }

    public Shader? Domain { get; set; }

    public Shader? Geometry { get; set; }

    public Shader Pixel { get; set; }

    public readonly bool Validate()
    {
        if (Vertex is null)
        {
            return false;
        }

        if (Pixel is null)
        {
            return false;
        }

        return true;
    }
}
