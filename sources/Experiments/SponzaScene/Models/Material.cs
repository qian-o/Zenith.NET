using System.Numerics;
using SharpGLTF.Schema2;
using Zenith.NET;
using Zenith.NET.Extensions.ImageSharp;
using GMaterial = SharpGLTF.Schema2.Material;
using GTexture = SharpGLTF.Schema2.Texture;
using Texture = Zenith.NET.Texture;

namespace SponzaScene.Models;

internal class Material : DisposableObject
{
    public Material(GMaterial material)
    {
        Name = material.Name ?? "Unnamed Material";
        DoubleSided = material.DoubleSided;
        AlphaCutoff = material.AlphaCutoff;
        BaseColorFactor = material.GetDiffuseColor(Vector4.One);

        if (material.GetDiffuseTexture() is GTexture texture)
        {
            using MemoryStream stream = new(texture.PrimaryImage.Content.Content.ToArray());

            BaseColorTexture = App.Context.LoadTextureFromStream(stream);
        }

        if (material.FindChannel("Normal")?.Texture is GTexture normalTexture)
        {
            using MemoryStream stream = new(normalTexture.PrimaryImage.Content.Content.ToArray());

            NormalTexture = App.Context.LoadTextureFromStream(stream, false);
        }

        if (material.FindChannel("MetallicRoughness") is MaterialChannel mr)
        {
            MetallicFactor = mr.GetFactor("MetallicFactor");
            RoughnessFactor = mr.GetFactor("RoughnessFactor");

            if (mr.Texture is GTexture metallicRoughnessTexture)
            {
                using MemoryStream stream = new(metallicRoughnessTexture.PrimaryImage.Content.Content.ToArray());

                MetallicRoughnessTexture = App.Context.LoadTextureFromStream(stream);
            }
        }

        if (material.FindChannel("Emissive") is MaterialChannel e)
        {
            EmissiveFactor = e.Color;
            EmissiveStrength = e.GetFactor("EmissiveStrength");
        }
    }

    public string Id { get; } = Guid.NewGuid().ToString();

    public string Name { get; }

    public bool DoubleSided { get; }

    public float AlphaCutoff { get; }

    public Vector4 BaseColorFactor { get; }

    public Texture? BaseColorTexture { get; }

    public Texture? NormalTexture { get; }

    public float MetallicFactor { get; }

    public float RoughnessFactor { get; }

    public Texture? MetallicRoughnessTexture { get; }

    public Vector4 EmissiveFactor { get; }

    public float EmissiveStrength { get; }

    protected override void Destroy()
    {
        MetallicRoughnessTexture?.Dispose();
        NormalTexture?.Dispose();
        BaseColorTexture?.Dispose();
    }
}
