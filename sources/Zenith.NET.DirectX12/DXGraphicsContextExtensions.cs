namespace Zenith.NET.DirectX12;

public static class DXGraphicsContextExtensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateDirectX12(bool useValidationLayer)
        {
            return new DXGraphicsContext(useValidationLayer);
        }
    }
}
