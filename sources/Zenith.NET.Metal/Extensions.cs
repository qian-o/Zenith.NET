namespace Zenith.NET;

public static class Extensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateMetal(bool useValidationLayer)
        {
            return new MTLGraphicsContext(useValidationLayer);
        }
    }
}
