using System.Diagnostics;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal static class Extensions
{
    extension(NSError error)
    {
        internal void Success()
        {
            if (!error.IsNull)
            {
                Debug.WriteLine($"Metal call failed with error: {error.LocalizedDescription}");
            }
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        internal MTL4CommandBuffer Metal()
        {
            return new(commandBuffer.GetNativeObject(NativeObjectType.MTL4CommandBuffer));
        }
    }

    extension(GraphicsContext context)
    {
        internal MTLDevice Metal()
        {
            return new(context.GetNativeObject(NativeObjectType.MTLDevice));
        }
    }

    extension(Texture texture)
    {
        internal MTLTexture Metal()
        {
            return new(texture.GetNativeObject(NativeObjectType.MTLTexture));
        }
    }
}
