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

        ColorAttachments = new VKTextureView[ColorAttachmentCount];

        RenderingAttachmentInfo* colorAttachmentInfos = (RenderingAttachmentInfo*)ZenithMarshal.Allocate<RenderingAttachmentInfo>(scope, ColorAttachmentCount);
        RenderingAttachmentInfo* depthStencilAttachmentInfo = HasDepthStencilAttachment ? (RenderingAttachmentInfo*)ZenithMarshal.Allocate<RenderingAttachmentInfo>(scope, 1) : null;

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

            VKTextureView textureView = new(context, attachment.Target, attachment.Slice);

            colorAttachmentInfos[i] = new()
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = textureView.ImageView,
                ImageLayout = ImageLayout.AttachmentOptimal,
                LoadOp = AttachmentLoadOp.Load,
                StoreOp = AttachmentStoreOp.Store
            };

            ColorAttachments[i] = textureView;
        }

        if (HasDepthStencilAttachment)
        {
            FrameBufferAttachment attachment = desc.DepthStencilAttachment!.Value;

            if (ColorAttachmentCount is 0)
            {
                ZenithHelper.MipDimensions(attachment.Target.Desc.Width, attachment.Target.Desc.Height, 0, attachment.Slice.MipLevel, out width, out height, out _);

                sampleCount = attachment.Target.Desc.SampleCount;
            }

            VKTextureView textureView = new(context, attachment.Target, attachment.Slice);

            depthStencilAttachmentInfo[0] = new()
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = textureView.ImageView,
                ImageLayout = ImageLayout.AttachmentOptimal,
                LoadOp = AttachmentLoadOp.Load,
                StoreOp = AttachmentStoreOp.Store
            };

            DepthStencilAttachment = textureView;
        }

        RenderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new() { Extent = new() { Width = width, Height = height } },
            LayerCount = 1,
            ColorAttachmentCount = ColorAttachmentCount,
            PColorAttachments = colorAttachmentInfos,
            PDepthAttachment = depthStencilAttachmentInfo,
            PStencilAttachment = depthStencilAttachmentInfo
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

    public VKTextureView[] ColorAttachments { get; }

    public VKTextureView? DepthStencilAttachment { get; }

    public void PrepareAttachmentsForRendering(VKCommandBuffer commandBuffer)
    {
        foreach (VKTextureView colorAttachment in ColorAttachments)
        {
            colorAttachment.TransitionLayout(commandBuffer, ImageLayout.AttachmentOptimal);
        }

        DepthStencilAttachment?.TransitionLayout(commandBuffer, ImageLayout.DepthStencilAttachmentOptimal);
    }

    public void FinalizeColorAttachmentsForPresent(VKCommandBuffer commandBuffer)
    {
        foreach (VKTextureView colorAttachment in ColorAttachments)
        {
            if (colorAttachment.Desc.Texture.Desc.Flags.HasFlag(TextureUsageFlags.RenderTarget))
            {
                colorAttachment.TransitionLayout(commandBuffer, ImageLayout.PresentSrcKhr);
            }
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DepthStencilAttachment?.Dispose();

        foreach (TextureView colorAttachment in ColorAttachments)
        {
            colorAttachment.Dispose();
        }

        scope.Dispose();
    }
}
