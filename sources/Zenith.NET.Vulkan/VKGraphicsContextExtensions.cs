namespace Zenith.NET.Vulkan;

public static class VKGraphicsContextExtensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateVulkan(bool useValidationLayer)
        {
            return new VKGraphicsContext(useValidationLayer);
        }
    }
}
