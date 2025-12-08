using SharpGLTF.Schema2;
using Zenith.NET;
using Material = SponzaScene.Models.Material;

namespace SponzaScene.Helpers;

internal class NewSponza(GraphicsContext context)
{
    public const string Directory = @"C:\Users\13247\OneDrive\NewSponza";

    public void Initialize()
    {
        LoadModel("NewSponza_Main");
        LoadModel("NewSponza_IvyGrowth");
        LoadModel("NewSponza_CypressTree");
        LoadModel("NewSponza_Curtains");
    }

    private void LoadModel(string name)
    {
        ModelRoot root = ModelRoot.Load(Path.Combine(Directory, name, name) + ".gltf");

        Material[] materials = [.. root.LogicalMaterials.Select(item => new Material(context, item))];
    }
}
