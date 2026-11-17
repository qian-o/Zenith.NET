using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSSuperResolution(DLSSVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)
{
    private readonly NGXParameter parameters = vendor.CreateParameters();

    private Key key;
    private NGXHandle handle;

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        if (Ensure(args))
        {
            args.Reset = true;
        }

        commandBuffer.Transition(Output, default, args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, parameters, handle, Desc, args, Output);

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        vendor.DestroyFeature(handle);
        vendor.DestroyParameters(parameters);

        base.Destroy();
    }

    private bool Ensure(SuperResolutionArgs args)
    {
        Key newKey = new(args);

        if (key.Equals(newKey))
        {
            return false;
        }

        key = newKey;

        vendor.DestroyFeature(handle);
        handle = vendor.CreateFeature(parameters, Desc, args);

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
