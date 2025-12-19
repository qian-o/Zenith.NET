using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXFrameBuffer : FrameBuffer
{
    private readonly ZenithMarshal.Scope scope = new();

    public CpuDescriptorHandle* RtvHandles;

    public CpuDescriptorHandle* DsvHandle;

    public DXFrameBuffer(DXGraphicsContext context, FrameBufferDesc desc) : base(context, desc)
    {
        ColorAttachmentCount = (uint)desc.ColorAttachments.Length;
        HasDepthStencilAttachment = desc.DepthStencilAttachment is not null;

        RtvHandles = (CpuDescriptorHandle*)ZenithMarshal.Allocate<CpuDescriptorHandle>(scope, ColorAttachmentCount);
        DsvHandle = HasDepthStencilAttachment ? (CpuDescriptorHandle*)ZenithMarshal.Allocate<CpuDescriptorHandle>(scope, 1) : null;

        Tokens = new DXDescriptorToken[ColorAttachmentCount + (HasDepthStencilAttachment ? 1 : 0)];

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

            RtvHandles[i] = (Tokens[i] = attachment.Target.DirectX12().CreateRtvToken(attachment.Slice)).Handle;
        }

        if (HasDepthStencilAttachment)
        {
            FrameBufferAttachment attachment = desc.DepthStencilAttachment!.Value;

            if (ColorAttachmentCount is 0)
            {
                ZenithHelper.MipDimensions(attachment.Target.Desc.Width, attachment.Target.Desc.Height, 0, attachment.Slice.MipLevel, out width, out height, out _);

                sampleCount = attachment.Target.Desc.SampleCount;
            }

            DsvHandle[0] = (Tokens[ColorAttachmentCount] = attachment.Target.DirectX12().CreateDsvToken(attachment.Slice)).Handle;
        }

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

    public DXDescriptorToken[] Tokens { get; }

    public void PrepareAttachmentsForRendering(CommandBuffer commandBuffer)
    {
        foreach (FrameBufferAttachment attachment in Desc.ColorAttachments)
        {
            attachment.Target.DirectX12().TransitionStates(commandBuffer, attachment.Slice, ResourceStates.RenderTarget);
        }

        Desc.DepthStencilAttachment?.Target.DirectX12().TransitionStates(commandBuffer, Desc.DepthStencilAttachment.Value.Slice, ResourceStates.DepthWrite | ResourceStates.DepthRead);
    }

    public void FinalizeColorAttachmentsForPresent(CommandBuffer commandBuffer)
    {
        foreach (FrameBufferAttachment attachment in Desc.ColorAttachments)
        {
            if (attachment.Target.Desc.Flags.HasFlag(TextureUsageFlags.RenderTarget))
            {
                attachment.Target.DirectX12().TransitionStates(commandBuffer, attachment.Slice, ResourceStates.Present);
            }
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        foreach (DXDescriptorToken token in Tokens)
        {
            token.Dispose();
        }

        scope.Dispose();
    }
}
