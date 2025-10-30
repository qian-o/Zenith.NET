namespace Zenith.NET;

public static class Extensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateVulkan(bool useValidationLayer)
        {
            return new VKGraphicsContext(useValidationLayer);
        }
    }
}
