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

            BaseColorTexture = App.Context.LoadTextureFromStream(stream, true);
        }

        if (material.FindChannel("Normal")?.Texture is GTexture normalTexture)
        {
            using MemoryStream stream = new(normalTexture.PrimaryImage.Content.Content.ToArray());

            NormalTexture = App.Context.LoadTextureFromStream(stream);
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

    public Material(string name,
                    bool doubleSided = false,
                    float alphaCutoff = 0.5f,
                    Vector4? baseColorFactor = null,
                    float metallicFactor = 1.0f,
                    float roughnessFactor = 1.0f,
                    Vector4? emissiveFactor = null,
                    float emissiveStrength = 1.0f)
    {
        Name = name;
        DoubleSided = doubleSided;
        AlphaCutoff = alphaCutoff;
        BaseColorFactor = baseColorFactor ?? Vector4.One;
        MetallicFactor = metallicFactor;
        RoughnessFactor = roughnessFactor;
        EmissiveFactor = emissiveFactor ?? Vector4.Zero;
        EmissiveStrength = emissiveStrength;
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
