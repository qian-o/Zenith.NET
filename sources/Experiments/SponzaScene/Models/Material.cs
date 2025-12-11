using System.Numerics;
using SharpGLTF.Schema2;
using Zenith.NET;
using Zenith.NET.Extensions.ImageSharp;
using GMaterial = SharpGLTF.Schema2.Material;

namespace SponzaScene.Models;

internal class Material(GMaterial material) : DisposableObject
{
    public string Name { get; } = material.Name ?? "Unnamed Material";

    public Vector4 DiffuseColor { get; } = material.GetDiffuseColor(Vector4.One);

    protected override void Destroy()
    {
    }
}
