using System.Numerics;
using Zenith.NET;

namespace Sponza.Models;

internal struct MaterialData
{
    public Vector4 BaseColorFactor;

    public float MetallicFactor;

    public float RoughnessFactor;

    public float NormalScale;

    public float AlphaCutoff;

    public bool DoubleSided;

    public bool AlphaMasked;

    public Texture? BaseColor;

    public Texture? MetallicRoughness;

    public Texture? Normal;
}
