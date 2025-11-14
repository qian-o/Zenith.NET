using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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

            Span<Rgba32> pixels = new Rgba32[image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            TextureSlice slice = default;
            TextureExtent extent = new() { Width = (uint)image.Width, Height = (uint)image.Height, Depth = 1 };

            texture.Upload(pixels, slice, default, extent);

            for (uint i = 1; i < mipLevels; i++)
            {
                ZenithHelper.MipDimensions((uint)image.Width, (uint)image.Height, 1, i, out uint mipWidth, out uint mipHeight, out _);

                using Image<Rgba32> mipImage = image.Clone(ctx => ctx.Resize((int)mipWidth, (int)mipHeight, KnownResamplers.MitchellNetravali));

                pixels = new Rgba32[mipWidth * mipHeight];
                mipImage.CopyPixelDataTo(pixels);

                slice.MipLevel = i;
                extent.Width = mipWidth;
                extent.Height = mipHeight;

                texture.Upload(pixels, slice, default, extent);
            }

            return texture;
        }
    }
}
