using System.Diagnostics;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal static class Extensions
{
    extension(CommandBuffer commandBuffer)
    {
        public MTL4CommandBuffer Metal()
        {
            return new(commandBuffer.GetNativeObject(NativeObjectType.MTL4CommandBuffer));
        }
    }

    extension(GraphicsContext context)
    {
        public MTLDevice Metal()
        {
            return new(context.GetNativeObject(NativeObjectType.MTLDevice));
        }
    }

    extension(NSError error)
    {
        public void Success()
        {
            if (!error.IsNull)
            {
                Debug.WriteLine($"Metal call failed with error: {error.LocalizedDescription}");
            }
        }
    }

    extension(Texture texture)
    {
        public MTLTexture Metal()
        {
            return new(texture.GetNativeObject(NativeObjectType.MTLTexture));
        }
    }
}
