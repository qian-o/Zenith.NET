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
        BaseColorFactor = material.GetDiffuseColor(Vector4.One);

        if (material.GetDiffuseTexture() is GTexture texture)
        {
            Dispatcher.Invoke(() => BaseColorTexture = App.Context.LoadTextureFromStream(new MemoryStream(texture.PrimaryImage.Content.Content.ToArray())));
        }

        if (material.FindChannel("Normal")?.Texture is GTexture normalTexture)
        {
            Dispatcher.Invoke(() => NormalTexture = App.Context.LoadTextureFromStream(new MemoryStream(normalTexture.PrimaryImage.Content.Content.ToArray()), false));
        }
    }

    public string Name { get; }

    public Vector4 BaseColorFactor { get; }

    public Texture? BaseColorTexture { get; private set; }

    public Texture? NormalTexture { get; private set; }

    protected override void Destroy()
    {
        NormalTexture?.Dispose();
        BaseColorTexture?.Dispose();
    }
}
