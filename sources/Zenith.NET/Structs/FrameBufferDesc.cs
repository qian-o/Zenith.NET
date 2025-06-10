using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct FrameBufferDesc : IDesc
{
    public FrameBufferDesc()
    {
        ColorTargets = [];
        DepthStencilTarget = null;
    }

    public FrameBufferAttachmentDesc[] ColorTargets { get; set; }

    public FrameBufferAttachmentDesc? DepthStencilTarget { get; set; }

    public readonly bool Validate()
    {
        if (!Validation.IsValidDescs(ColorTargets, emptyAllowed: true))
        {
            return false;
        }

        if (DepthStencilTarget?.Validate() is false)
        {
            return false;
        }

        return true;
    }
}
