using Zenith.NET;

namespace SponzaScene.Helpers;

internal class NewSponza(GraphicsContext context)
{
    public const string Directory = @"C:\Users\13247\OneDrive\NewSponza";

    public void Initialize()
    {
        LoadModel("NewSponza_Main");
    }

    private void LoadModel(string name)
    {
        string path = Path.Combine(Directory, name);
    }
}
