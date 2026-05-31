using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Zenith.NET.Extensions.ImageSharp;

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public Texture LoadTextureFromStream(Stream stream, bool generateMipMaps = true)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);

            uint mipLevels = generateMipMaps ? ZenithHelper.MipLevels((uint)image.Width, (uint)image.Height, 1) : 1;

            Texture texture = context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm,
                                                                          (uint)image.Width,
                                                                          (uint)image.Height,
                                                                          mipLevels,
                                                                          SampleCount.Count1));

            Rgba32[] pixels = new Rgba32[image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            CommandBuffer commandBuffer = context.CopyQueue.AcquireCommandBuffer();

            unsafe
            {
                fixed (Rgba32* pPixels = pixels)
                {
                    Extent3D extent = new() { Width = (uint)image.Width, Height = (uint)image.Height, Depth = 1 };

                    TextureData data = new()
                    {
                        Pointer = (nint)pPixels,
                        SizeInBytes = (uint)(sizeof(Rgba32) * pixels.Length),
                        RowStrideInBytes = ZenithHelper.RowStrideInBytes(PixelFormat.R8G8B8A8UNorm, extent.Width, extent.Height),
                        SliceStrideInBytes = ZenithHelper.SliceStrideInBytes(PixelFormat.R8G8B8A8UNorm, extent.Width, extent.Height)
                    };

                    commandBuffer.Upload(texture, default, default, extent, data);
                }

                for (uint i = 1; i < mipLevels; i++)
                {
                    ZenithHelper.MipDimensions((uint)image.Width, (uint)image.Height, 1, i, out uint mipWidth, out uint mipHeight, out _);

                    using Image<Rgba32> mipImage = image.Clone(ctx => ctx.Resize((int)mipWidth, (int)mipHeight, KnownResamplers.MitchellNetravali));

                    pixels = new Rgba32[mipWidth * mipHeight];
                    mipImage.CopyPixelDataTo(pixels);

                    fixed (Rgba32* pPixels = pixels)
                    {
                        Extent3D extent = new() { Width = mipWidth, Height = mipHeight, Depth = 1 };

                        TextureData data = new()
                        {
                            Pointer = (nint)pPixels,
                            SizeInBytes = (uint)(sizeof(Rgba32) * pixels.Length),
                            RowStrideInBytes = ZenithHelper.RowStrideInBytes(PixelFormat.R8G8B8A8UNorm, extent.Width, extent.Height),
                            SliceStrideInBytes = ZenithHelper.SliceStrideInBytes(PixelFormat.R8G8B8A8UNorm, extent.Width, extent.Height)
                        };

                        commandBuffer.Upload(texture, new() { MipLevel = i }, default, extent, data);
                    }
                }
            }

            commandBuffer.Submit().Wait();

            return texture;
        }

        public Texture LoadTextureFromFile(string file, bool generateMipMaps = true)
        {
            using FileStream stream = File.OpenRead(file);

            return context.LoadTextureFromStream(stream, generateMipMaps);
        }
    }
}
