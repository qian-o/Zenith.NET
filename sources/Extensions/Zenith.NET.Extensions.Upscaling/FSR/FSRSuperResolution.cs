using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal class FSRSuperResolution(FSRVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)
{
    private FSRContext context;

    protected override void Build(SuperResolutionArgs args)
    {
        vendor.DestroyFeature(context);
        context = vendor.CreateFeature(Desc, args);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        FSRVendor.Transition(commandBuffer, [args.Color, args.Depth, args.MotionVectors, args.ReactiveMask, args.Exposure], TextureLayout.Sampled, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [Output], args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Common);

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, context, args, null, Output);

        commandBuffer.EndExternalCommands();

        FSRVendor.Transition(commandBuffer, [args.Color, args.Depth, args.MotionVectors, args.ReactiveMask, args.Exposure, Output], TextureLayout.Common, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        vendor.DestroyFeature(context);

        base.Destroy();
    }
}
