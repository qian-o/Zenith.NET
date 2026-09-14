using Zenith.NET;

namespace CornellBox.Passes;

internal abstract class Pass : DisposableObject
{
    protected static string ShaderPath(string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", file);
    }

    protected static Texture CreateTexture(uint width, uint height, PixelFormat format)
    {
        return App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = format,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }
}
