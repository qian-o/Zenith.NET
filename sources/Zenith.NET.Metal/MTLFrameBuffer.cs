using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLFrameBuffer : FrameBuffer
{
    public MTL4RenderPassDescriptor Descriptor;

    public MTLFrameBuffer(MTLGraphicsContext context, FrameBufferDesc desc) : base(context, desc)
    {
        ColorAttachmentCount = (uint)desc.ColorAttachments.Length;
        HasDepthStencilAttachment = desc.DepthStencilAttachment is not null;

        Descriptor = new();

        uint width = 0;
        uint height = 0;
        SampleCount sampleCount = SampleCount.Count1;

        for (uint i = 0; i < ColorAttachmentCount; i++)
        {
            FrameBufferAttachment attachment = desc.ColorAttachments[i];

            if (i is 0)
            {
                ZenithHelper.MipDimensions(attachment.Target.Desc.Width, attachment.Target.Desc.Height, 0, attachment.Slice.MipLevel, out width, out height, out _);

                sampleCount = attachment.Target.Desc.SampleCount;
            }

            Descriptor.ColorAttachments[i] = new()
            {
                Texture = attachment.Target.Metal().Texture,
                Level = attachment.Slice.MipLevel,
                Slice = ZenithHelper.FlattenArrayLayerIndex(attachment.Target.Desc, attachment.Slice),
                LoadAction = MTLLoadAction.Load,
                StoreAction = MTLStoreAction.Store
            };
        }

        if (HasDepthStencilAttachment)
        {
            FrameBufferAttachment attachment = desc.DepthStencilAttachment!.Value;

            if (ColorAttachmentCount is 0)
            {
                ZenithHelper.MipDimensions(attachment.Target.Desc.Width, attachment.Target.Desc.Height, 0, attachment.Slice.MipLevel, out width, out height, out _);

                sampleCount = attachment.Target.Desc.SampleCount;
            }

            if (ZenithHelper.HasDepth(attachment.Target.Desc.Format))
            {
                Descriptor.DepthAttachment = new()
                {
                    Texture = attachment.Target.Metal().Texture,
                    Level = attachment.Slice.MipLevel,
                    Slice = ZenithHelper.FlattenArrayLayerIndex(attachment.Target.Desc, attachment.Slice),
                    LoadAction = MTLLoadAction.Load,
                    StoreAction = MTLStoreAction.Store
                };
            }

            if (ZenithHelper.HasStencil(attachment.Target.Desc.Format))
            {
                Descriptor.StencilAttachment = new()
                {
                    Texture = attachment.Target.Metal().Texture,
                    Level = attachment.Slice.MipLevel,
                    Slice = ZenithHelper.FlattenArrayLayerIndex(attachment.Target.Desc, attachment.Slice),
                    LoadAction = MTLLoadAction.Load,
                    StoreAction = MTLStoreAction.Store
                };
            }
        }

        Descriptor.RenderTargetWidth = width;
        Descriptor.RenderTargetHeight = height;

        Width = width;
        Height = height;
        Output = new()
        {
            ColorAttachments = [.. desc.ColorAttachments.Select(static item => item.Target.Desc.Format)],
            DepthStencilAttachment = desc.DepthStencilAttachment?.Target.Desc.Format,
            SampleCount = sampleCount
        };
    }

    public override uint ColorAttachmentCount { get; }

    public override bool HasDepthStencilAttachment { get; }

    public override uint Width { get; }

    public override uint Height { get; }

    public override Output Output { get; }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
