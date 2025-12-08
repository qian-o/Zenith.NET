using SharpGLTF.Schema2;
using Material = SponzaScene.Models.Material;

namespace SponzaScene.Helpers;

internal class NewSponza
{
    public const string Directory = @"C:\Users\13247\OneDrive\NewSponza";

    public NewSponza()
    {
        string[] modelNames = ["NewSponza_Main", "NewSponza_IvyGrowth", "NewSponza_CypressTree", "NewSponza_Curtains"];

        Parallel.ForEach(modelNames, LoadModel);
    }

    private void LoadModel(string name)
    {
        ModelRoot root = ModelRoot.Load(Path.Combine(Directory, name, name) + ".gltf");

        Material[] materials = [.. root.LogicalMaterials.Select(static item => new Material(item))];
    }
}
