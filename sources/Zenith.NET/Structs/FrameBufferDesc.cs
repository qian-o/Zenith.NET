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
        if (ColorTargets is null)
        {
            return false;
        }

        if (ColorTargets.Any(static item => !item.Validate()))
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
