using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

public static class Extensions
{
    private readonly static Dictionary<GraphicsContext, GRContext> grContexts = [];

    extension(GraphicsContext context)
    {
        public SKTexture CreateSKTexture(SKTextureDesc desc)
        {
            if (!grContexts.TryGetValue(context, out GRContext? grContext))
            {
                grContexts.Add(context, grContext = GRContext.CreateGl());
            }

            return new(context, grContext, desc);
        }
    }
}
