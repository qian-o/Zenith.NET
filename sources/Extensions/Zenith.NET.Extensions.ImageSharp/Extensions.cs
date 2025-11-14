using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Zenith.NET.Extensions.ImageSharp;

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public Texture LoadTextureFromFile(string file, bool generateMipMaps = true)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(file);

            uint mipLevels = generateMipMaps ? ZenithHelper.MipLevels((uint)image.Width, (uint)image.Height, 1) : 1;

            Texture texture = context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R8G8B8A8UNorm,
                Width = (uint)image.Width,
                Height = (uint)image.Height,
                Depth = 1,
                MipLevels = mipLevels,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.ShaderResource
            });

            for (uint i = 0; i < mipLevels; i++)
            {
            }

            return texture;
        }
    }
}
