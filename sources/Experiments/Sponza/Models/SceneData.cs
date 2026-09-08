using System.Numerics;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Models;

internal struct SceneData
{
    public Buffer VertexBuffer;

    public Buffer IndexBuffer;

    public Buffer MaterialBuffer;

    public Texture[] Textures;

    public DrawData[] Draws;

    public MaterialData[] Materials;

    public Vector3 BoundsMinimumWorld;

    public Vector3 BoundsMaximumWorld;
}
