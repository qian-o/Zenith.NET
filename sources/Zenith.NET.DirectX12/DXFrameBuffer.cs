using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXFrameBuffer : FrameBuffer
{
    private readonly ZenithMarshal.Scope scope = new();

    public RenderPassRenderTargetDesc* RenderTargets;

    public RenderPassDepthStencilDesc* DepthStencil;

    public DXFrameBuffer(DXGraphicsContext context, FrameBufferDesc desc) : base(context, desc)
    {
        ColorAttachmentCount = (uint)desc.ColorAttachments.Length;
        HasDepthStencilAttachment = desc.DepthStencilAttachment is not null;

        RenderTargets = (RenderPassRenderTargetDesc*)ZenithMarshal.Allocate<RenderPassRenderTargetDesc>(scope, ColorAttachmentCount);
        DepthStencil = HasDepthStencilAttachment ? (RenderPassDepthStencilDesc*)ZenithMarshal.Allocate<RenderPassDepthStencilDesc>(scope, 1) : null;

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

            RenderTargets[i] = new()
            {
                CpuDescriptor = (Tokens[i] = attachment.Target.DirectX12().CreateRtvToken(attachment.Slice)).Handle,
                BeginningAccess = new()
                {
                    Type = RenderPassBeginningAccessType.Preserve,
                    Clear = new()
                    {
                        ClearValue = new()
                        {
                            Format = DXFormats.DirectX12(attachment.Target.Desc.Format)
                        }
                    }
                },
                EndingAccess = new()
                {
                    Type = RenderPassEndingAccessType.Preserve
                }
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

            bool hasDepth = ZenithHelper.HasDepth(attachment.Target.Desc.Format);
            bool hasStencil = ZenithHelper.HasStencil(attachment.Target.Desc.Format);

            DepthStencil[0] = new()
            {
                CpuDescriptor = (Tokens[ColorAttachmentCount] = attachment.Target.DirectX12().CreateDsvToken(attachment.Slice)).Handle,
                DepthBeginningAccess = new()
                {
                    Type = hasDepth ? RenderPassBeginningAccessType.Preserve : RenderPassBeginningAccessType.NoAccess,
                    Clear = new()
                    {
                        ClearValue = new()
                        {
                            Format = DXFormats.DirectX12(attachment.Target.Desc.Format)
                        }
                    }
                },
                StencilBeginningAccess = new()
                {
                    Type = hasStencil ? RenderPassBeginningAccessType.Preserve : RenderPassBeginningAccessType.NoAccess,
                    Clear = new()
                    {
                        ClearValue = new()
                        {
                            Format = DXFormats.DirectX12(attachment.Target.Desc.Format)
                        }
                    }
                },
                DepthEndingAccess = new()
                {
                    Type = hasDepth ? RenderPassEndingAccessType.Preserve : RenderPassEndingAccessType.NoAccess
                },
                StencilEndingAccess = new()
                {
                    Type = hasStencil ? RenderPassEndingAccessType.Preserve : RenderPassEndingAccessType.NoAccess
                }
            };
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

    public void PrepareAttachments(DXCommandBuffer commandBuffer)
    {
        foreach (FrameBufferAttachment attachment in Desc.ColorAttachments)
        {
            attachment.Target.DirectX12().TransitionStates(commandBuffer, attachment.Slice, ResourceStates.RenderTarget);
        }

        Desc.DepthStencilAttachment?.Target.DirectX12().TransitionStates(commandBuffer, Desc.DepthStencilAttachment.Value.Slice, ResourceStates.DepthWrite);
    }

    public void PresentColorAttachments(DXCommandBuffer commandBuffer)
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
