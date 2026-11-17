using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSSuperResolution(DLSSVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)
{
    private Key key;

    public readonly NGXParameter Parameters = vendor.AllocateParameters();

    public NGXHandle Handle;

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        commandBuffer.Transition(Output, default, key.Color is PixelFormat.Unknown ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        if (Ensure(commandBuffer, args))
        {
            args.Reset = true;
        }

        vendor.Evaluate(commandBuffer, this, args);

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        vendor.ReleaseFeature(Handle);
        vendor.DestroyParameters(Parameters);

        base.Destroy();
    }

    private bool Ensure(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        Key newKey = new(args);

        if (key.Equals(newKey))
        {
            return false;
        }

        key = newKey;

        vendor.Create(commandBuffer, this, args);

        return true;
    }

    private readonly struct Key(SuperResolutionArgs args)
    {
        public readonly PixelFormat Color = args.Color.Desc.Format;

        public readonly PixelFormat Depth = args.Depth.Desc.Format;

        public readonly PixelFormat MotionVectors = args.MotionVectors.Desc.Format;

        public readonly PixelFormat ReactiveMask = args.ReactiveMask?.Desc.Format ?? default;

        public readonly PixelFormat Exposure = args.Exposure?.Desc.Format ?? default;

        public readonly bool DepthReversed = UpscalingHelper.IsDepthReversed(args.Projection);

        public bool Equals(Key other)
        {
            return Color == other.Color && Depth == other.Depth && MotionVectors == other.MotionVectors && ReactiveMask == other.ReactiveMask && Exposure == other.Exposure && DepthReversed == other.DepthReversed;
        }
    }
}
