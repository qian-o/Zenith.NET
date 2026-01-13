using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Zenith.NET.Extensions.ImageSharp;

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public Texture LoadTextureFromStream(Stream stream, bool generateMipMaps = false)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);

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

            Rgba32[] pixels = new Rgba32[image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            CommandBuffer commandBuffer = context.Copy.CommandBuffer();

            commandBuffer.Upload(texture, default, default, new() { Width = (uint)image.Width, Height = (uint)image.Height, Depth = 1 }, pixels);

            for (uint i = 1; i < mipLevels; i++)
            {
                ZenithHelper.MipDimensions((uint)image.Width, (uint)image.Height, 1, i, out uint mipWidth, out uint mipHeight, out _);

                using Image<Rgba32> mipImage = image.Clone(ctx => ctx.Resize((int)mipWidth, (int)mipHeight, KnownResamplers.MitchellNetravali));

                pixels = new Rgba32[mipWidth * mipHeight];
                mipImage.CopyPixelDataTo(pixels);

                commandBuffer.Upload(texture, new() { MipLevel = i }, default, new() { Width = mipWidth, Height = mipHeight, Depth = 1 }, pixels);
            }

            commandBuffer.Submit(true);

            return texture;
        }

        public Texture LoadTextureFromFile(string file, bool generateMipMaps = true)
        {
            using FileStream stream = File.OpenRead(file);

            return context.LoadTextureFromStream(stream, generateMipMaps);
        }
    }
}
