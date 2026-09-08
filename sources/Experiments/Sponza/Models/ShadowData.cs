using System.Numerics;
using Zenith.NET;

namespace Sponza.Models;

internal struct ShadowData
{
    public Texture Texture;

    public Matrix4x4 ViewProjection;

    public float NormalBiasInMeters;

    public float DepthBias;

    public float TexelSize;
}
