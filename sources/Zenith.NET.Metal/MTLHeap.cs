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
            ResourceOptions = MTLFormats.Metal(desc.Flags),
            Size = context.Device.HeapBufferSizeAndAlign(desc.SizeInBytes, MTLFormats.Metal(desc.Flags)).Size
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
            SampleCount = MTLFormats.Metal(desc.SampleCount),
            ArrayLength = desc.ArrayLayers,
            ResourceOptions = MTLResourceOptions.StorageModePrivate | MTLResourceOptions.HazardTrackingModeUntracked,
            AllowGPUOptimizedContents = true,
            Usage = MTLFormats.Metal(desc.Flags)
        };

        MTLHeapDescriptor descriptor = new()
        {
            Type = MTLHeapType.Automatic,
            ResourceOptions = MTLResourceOptions.StorageModePrivate | MTLResourceOptions.HazardTrackingModeUntracked,
            Size = context.Device.HeapTextureSizeAndAlign(textureDescriptor).Size
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
