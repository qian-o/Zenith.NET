using SponzaScene.Models;
using Zenith.NET;

namespace SponzaScene.Helpers;

internal unsafe class NewSponza : DisposableObject
{
    public const string Directory = @"C:\Users\13247\OneDrive\NewSponza";

    public NewSponza()
    {
        Main = new(Path("NewSponza_Main"));
        IvyGrowth = new(Path("NewSponza_IvyGrowth"));
        CypressTree = new(Path("NewSponza_CypressTree"));
        Curtains = new(Path("NewSponza_Curtains"));
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

    private static string Path(string name)
    {
        return System.IO.Path.Combine(Directory, name, $"{name}.gltf");
    }
}
