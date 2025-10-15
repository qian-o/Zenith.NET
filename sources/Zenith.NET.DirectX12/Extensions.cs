namespace Zenith.NET;

public static class Extensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateDirectX12(bool useValidationLayer)
        {
            return new DXGraphicsContext(useValidationLayer);
        }
    }
}
