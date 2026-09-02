namespace Zenith.NET.Extensions.Upscaling;

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public Upscaler CreateUpscaler(UpscalerDesc desc)
        {
            return new(context, desc);
        }
    }
}