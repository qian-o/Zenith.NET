namespace Zenith.NET.Extensions.Upscaling;

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public SpatialUpscaler CreateSpatialUpscaler(SpatialUpscalerDesc desc)
        {
            return new(context, desc);
        }

        public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
        {
            return new(context, desc);
        }
    }
}
