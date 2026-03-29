using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLHeap : GraphicsResource
{
    public MtlHeap Heap;

    public MTLHeap(MTLGraphicsContext context, BufferDesc desc, out MtlBuffer buffer) : base(context)
    {
        context.AddAllocation(Heap = context.Device.MakeHeap(new()
        {
            Size = context.Device.HeapBufferSizeAndAlign(desc.SizeInBytes, MTLFormats.Metal(desc.Flags)).Size,
            ResourceOptions = MTLFormats.Metal(desc.Flags),
            Type = MTLHeapType.Automatic
        }));

        buffer = Heap.MakeBuffer(desc.SizeInBytes, MTLFormats.Metal(desc.Flags));
    }

    public MTLHeap(MTLGraphicsContext context, TextureDesc desc, out MtlTexture texture) : base(context)
    {
        MTLTextureDescriptor descriptor = new()
        {
            TextureType = MTLFormats.Metal(desc.Type),
            PixelFormat = MTLFormats.Metal(desc.Format).PixelFormat,
            Width = desc.Width,
            Height = desc.Height,
            Depth = desc.Depth,
            MipmapLevelCount = desc.MipLevels,
            SampleCount = MTLFormats.Metal(desc.SampleCount),
            ArrayLength = desc.ArrayLayers,
            ResourceOptions = MTLResourceOptions.StorageModePrivate | MTLResourceOptions.HazardTrackingModeUntracked,
            Usage = MTLFormats.Metal(desc.Flags),
            AllowGPUOptimizedContents = true
        };

        context.AddAllocation(Heap = context.Device.MakeHeap(new()
        {
            Size = context.Device.HeapTextureSizeAndAlign(descriptor).Size,
            ResourceOptions = descriptor.ResourceOptions,
            Type = MTLHeapType.Automatic
        }));

        texture = Heap.MakeTexture(descriptor);
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
