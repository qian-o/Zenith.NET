using System.Numerics;
using Zenith.NET;

namespace SponzaScene.Models;

internal struct Vertex
{
    public Vector3 Position;

    public Vector3 Normal;

    public Vector2 TexCoord;

    public Vector4 Color;

    public static InputLayout InputLayout()
    {
        InputLayout layout = new();
        layout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
        layout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Normal });
        layout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.TexCoord });
        layout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Color });

        return layout;
    }
}
