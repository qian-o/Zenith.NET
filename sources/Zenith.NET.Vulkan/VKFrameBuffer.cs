using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKFrameBuffer : FrameBuffer
{
    private readonly ZenithMarshal.Scope scope = new();

    public RenderingInfo RenderingInfo;

    public VKFrameBuffer(VKGraphicsContext context, FrameBufferDesc desc) : base(context, desc)
    {
        ColorAttachmentCount = (uint)desc.ColorAttachments.Length;
        HasDepthStencilAttachment = desc.DepthStencilAttachment is not null;

        RenderingAttachmentInfo* colorAttachmentInfos = (RenderingAttachmentInfo*)ZenithMarshal.Allocate<RenderingAttachmentInfo>(scope, ColorAttachmentCount);
        RenderingAttachmentInfo* depthStencilAttachmentInfo = HasDepthStencilAttachment ? (RenderingAttachmentInfo*)ZenithMarshal.Allocate<RenderingAttachmentInfo>(scope, 1) : null;

        ImageViews = new ImageView[ColorAttachmentCount + (HasDepthStencilAttachment ? 1 : 0)];

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

            colorAttachmentInfos[i] = new()
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = ImageViews[i] = attachment.Target.Vulkan().CreateAttachmentView(attachment.Slice),
                ImageLayout = ImageLayout.AttachmentOptimal,
                LoadOp = AttachmentLoadOp.Load,
                StoreOp = AttachmentStoreOp.Store
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

            depthStencilAttachmentInfo[0] = new()
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = ImageViews[ColorAttachmentCount] = attachment.Target.Vulkan().CreateAttachmentView(attachment.Slice),
                ImageLayout = ImageLayout.AttachmentOptimal,
                LoadOp = AttachmentLoadOp.Load,
                StoreOp = AttachmentStoreOp.Store
            };
        }

        RenderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new() { Extent = new() { Width = width, Height = height } },
            LayerCount = 1,
            ColorAttachmentCount = ColorAttachmentCount,
            PColorAttachments = colorAttachmentInfos,
            PDepthAttachment = depthStencilAttachmentInfo,
            PStencilAttachment = desc.DepthStencilAttachment?.Target.Desc.Format is PixelFormat.D24UNormS8UInt or PixelFormat.D32FloatS8UInt ? depthStencilAttachmentInfo : null
        };

        Width = width;
        Height = height;
        Output = new()
        {
            ColorAttachments = [.. desc.ColorAttachments.Select(static item => item.Target.Desc.Format)],
            DepthStencilAttachment = desc.DepthStencilAttachment?.Target.Desc.Format,
            SampleCount = sampleCount
        };
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override uint ColorAttachmentCount { get; }

    public override bool HasDepthStencilAttachment { get; }

    public override uint Width { get; }

    public override uint Height { get; }

    public override Output Output { get; }

    public ImageView[] ImageViews { get; }

    public void PrepareAttachmentsForRendering(VKCommandBuffer commandBuffer)
    {
        foreach (FrameBufferAttachment attachment in Desc.ColorAttachments)
        {
            attachment.Target.Vulkan().TransitionLayout(commandBuffer, attachment.Slice, ImageLayout.ColorAttachmentOptimal);
        }

        Desc.DepthStencilAttachment?.Target.Vulkan().TransitionLayout(commandBuffer, Desc.DepthStencilAttachment.Value.Slice, ImageLayout.DepthStencilAttachmentOptimal);
    }

    public void FinalizeColorAttachmentsForPresent(VKCommandBuffer commandBuffer)
    {
        foreach (FrameBufferAttachment attachment in Desc.ColorAttachments)
        {
            if (attachment.Target.Desc.Flags.HasFlag(TextureUsageFlags.RenderTarget))
            {
                attachment.Target.Vulkan().TransitionLayout(commandBuffer, attachment.Slice, ImageLayout.PresentSrcKhr);
            }
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        foreach (ImageView imageView in ImageViews)
        {
            Context.Vk.DestroyImageView(Context.Device, imageView, null);
        }

        scope.Dispose();
    }
}
