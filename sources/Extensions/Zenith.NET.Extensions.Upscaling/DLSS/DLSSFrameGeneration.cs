using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSFrameGeneration(DLSSVendor vendor, FrameGenerationDesc desc) : FrameGeneration(vendor.Context, desc)
{
    private readonly NGXParameter parameters = vendor.CreateParameters();

    private NGXHandle handle;
    private Matrix4x4 previousViewProjection;

    protected override void Build(FrameGenerationArgs args)
    {
        vendor.DestroyFeature(handle);
        handle = vendor.CreateFeature(parameters, Desc);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.Transition(Output, default, args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, parameters, handle, args, Output, previousViewProjection);

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);

        previousViewProjection = args.View * args.Projection;
    }

    protected override void Destroy()
    {
        vendor.DestroyFeature(handle);
        vendor.DestroyParameters(parameters);

        base.Destroy();
    }
}
