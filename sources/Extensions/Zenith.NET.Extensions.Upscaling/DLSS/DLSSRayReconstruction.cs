using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSRayReconstruction(DLSSVendor vendor, RayReconstructionDesc desc) : RayReconstruction(vendor.Context, desc)
{
    private readonly NGXParameter parameters = vendor.CreateParameters();

    private NGXHandle handle;

    protected override void RebuildImpl(RayReconstructionArgs args)
    {
        vendor.DestroyFeature(handle);
        handle = vendor.CreateFeature(parameters, Desc, args);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        commandBuffer.Transition(Output, default, args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, parameters, handle, args, Output);

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        vendor.DestroyFeature(handle);
        vendor.DestroyParameters(parameters);

        base.Destroy();
    }
}
