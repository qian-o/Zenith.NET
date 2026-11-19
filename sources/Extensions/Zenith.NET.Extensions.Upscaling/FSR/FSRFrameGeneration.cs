using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal class FSRFrameGeneration(FSRVendor vendor, FrameGenerationDesc desc) : FrameGeneration(vendor.Context, desc)
{
    private FSRContext context;
    private ulong frameId;

    protected override void Build(FrameGenerationArgs args)
    {
        frameId = 0;

        vendor.DestroyFeature(context);
        context = vendor.CreateFeature(Desc, args);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        FSRVendor.Transition(commandBuffer, [args.Color, args.Depth, args.MotionVectors, args.Hudless], TextureLayout.Sampled, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [Output], args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Common);

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, context, args, Output, ++frameId);

        commandBuffer.EndExternalCommands();

        FSRVendor.Transition(commandBuffer, [args.Color, args.Depth, args.MotionVectors, args.Hudless, Output], TextureLayout.Common, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        vendor.DestroyFeature(context);

        base.Destroy();
    }
}
