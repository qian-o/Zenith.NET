using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLHeap : GraphicsResource
{
    public MtlHeap Heap;

    public MTLHeap(MTLGraphicsContext context, BufferDesc desc, out MtlBuffer buffer) : base(context)
    {
        MTLHeapDescriptor descriptor = new()
        {
            Type = MTLHeapType.Automatic,
            Size = context.Device.HeapBufferSizeAndAlign(desc.SizeInBytes, MTLFormats.Metal(desc.Flags)).Size,
            ResourceOptions = MTLFormats.Metal(desc.Flags)
        };

        context.AddAllocation(Heap = context.Device.NewHeap(descriptor));

        buffer = Heap.NewBuffer(desc.SizeInBytes, MTLFormats.Metal(desc.Flags));
    }

    public MTLHeap(MTLGraphicsContext context, TextureDesc desc, out MtlTexture texture) : base(context)
    {
        MTLTextureDescriptor textureDescriptor = new()
        {
            TextureType = MTLFormats.Metal(desc.Type),
            PixelFormat = MTLFormats.Metal(desc.Format),
            Width = desc.Width,
            Height = desc.Height,
            Depth = desc.Depth,
            MipmapLevelCount = desc.MipLevels,
            ArrayLength = desc.ArrayLayers,
            SampleCount = MTLFormats.Metal(desc.SampleCount),
            Usage = MTLFormats.Metal(desc.Flags),
            AllowGPUOptimizedContents = true,
            ResourceOptions = MTLResourceOptions.StorageModePrivate | MTLResourceOptions.HazardTrackingModeUntracked
        };

        MTLHeapDescriptor descriptor = new()
        {
            Type = MTLHeapType.Automatic,
            Size = context.Device.HeapTextureSizeAndAlign(textureDescriptor).Size,
            ResourceOptions = MTLResourceOptions.StorageModePrivate | MTLResourceOptions.HazardTrackingModeUntracked
        };

        context.AddAllocation(Heap = context.Device.NewHeap(descriptor));

        texture = Heap.NewTexture(textureDescriptor);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.RemoveAllocation(Heap);

        Heap.Dispose();
    }
}
