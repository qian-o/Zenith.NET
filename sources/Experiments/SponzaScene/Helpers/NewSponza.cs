using SponzaScene.Models;
using Zenith.NET;

namespace SponzaScene.Helpers;

internal unsafe class NewSponza : DisposableObject
{
    public const string Directory = @"C:\Users\13247\OneDrive\NewSponza";

    public NewSponza()
    {
        Main = new(PathCombine("NewSponza_Main"));
        IvyGrowth = new(PathCombine("NewSponza_IvyGrowth"));
        CypressTree = new(PathCombine("NewSponza_CypressTree"));
        Curtains = new(PathCombine("NewSponza_Curtains"));
    }

    public GLTF Main { get; }

    public GLTF IvyGrowth { get; }

    public GLTF CypressTree { get; }

    public GLTF Curtains { get; }

    protected override void Destroy()
    {
        Main.Dispose();
        IvyGrowth.Dispose();
        CypressTree.Dispose();
        Curtains.Dispose();
    }

    private static string PathCombine(string name)
    {
        return Path.Combine(Directory, name, $"{name}.gltf");
    }
}
