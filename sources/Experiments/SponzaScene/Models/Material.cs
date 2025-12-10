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
        DiffuseColor = material.GetDiffuseColor(Vector4.One);

        Dispatcher.Invoke(() =>
        {
            if (material.GetDiffuseTexture() is GTexture diffuseTexture)
            {
                using Stream stream = diffuseTexture.PrimaryImage.Content.Open();

                DiffuseTexture = App.Context.LoadTextureFromStream(stream);
            }
        });
    }

    public string Name { get; }

    public Vector4 DiffuseColor { get; }

    public Texture? DiffuseTexture { get; private set; }

    protected override void Destroy()
    {
        DiffuseTexture?.Dispose();
    }
}
