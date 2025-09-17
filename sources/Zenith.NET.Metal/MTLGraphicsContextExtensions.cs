namespace Zenith.NET.Metal;

public static class MTLGraphicsContextExtensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateMetal(bool useValidationLayer)
        {
            return new MTLGraphicsContext(useValidationLayer);
        }
    }
}
