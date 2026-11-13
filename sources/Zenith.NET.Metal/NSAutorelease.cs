using Metal.NET;

namespace Zenith.NET.Metal;

internal static class NSAutorelease
{
    public static MTL4ComputeCommandEncoder MakeComputeCommandEncoder(MTL4CommandBuffer commandBuffer)
    {
        using NSAutoreleasePool _ = new();

        return commandBuffer.MakeComputeCommandEncoder().Retain();
    }

    public static MTL4RenderCommandEncoder MakeRenderCommandEncoder(MTL4CommandBuffer commandBuffer, MTL4RenderPassDescriptor descriptor)
    {
        using NSAutoreleasePool _ = new();

        return commandBuffer.MakeRenderCommandEncoder(descriptor).Retain();
    }

    public static CAMetalDrawable NextDrawable(CAMetalLayer metalLayer)
    {
        using NSAutoreleasePool _ = new();

        return metalLayer.NextDrawable().Retain();
    }
}
