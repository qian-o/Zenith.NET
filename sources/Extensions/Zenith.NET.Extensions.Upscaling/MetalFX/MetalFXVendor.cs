using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXVendor : Vendor
{
    private readonly (float Min, float Max) superResolutionScale;
    private readonly (float Min, float Max) rayReconstructionScale;

    public MTLDevice Device;

    public MTL4Compiler Compiler;

    public MetalFXVendor(GraphicsContext context)
    {
        Context = context;

        if (!OperatingSystem.IsMacOSVersionAtLeast(27) && !OperatingSystem.IsIOSVersionAtLeast(27) && !OperatingSystem.IsTvOSVersionAtLeast(27, 1))
        {
            Capabilities = new(false, false, false);

            return;
        }

        Device = new(context.GetNativeObject(NativeObjectType.MTLDevice));
        Capabilities = new(MTLFXTemporalScalerDescriptor.SupportsMetal4FX(Device),
                           MTLFXTemporalDenoisedScalerDescriptor.SupportsMetal4FX(Device),
                           MTLFXFrameInterpolatorDescriptor.SupportsMetal4FX(Device));

        if (!Capabilities.SuperResolutionSupported && !Capabilities.RayReconstructionSupported && !Capabilities.FrameGenerationSupported)
        {
            return;
        }

        using MTL4CompilerDescriptor compilerDescriptor = new();
        Compiler = Device.MakeCompiler(compilerDescriptor, out NSError error);
        error.Success();

        if (Compiler.IsNull)
        {
            Capabilities = new(false, false, false);

            return;
        }

        if (Capabilities.SuperResolutionSupported)
        {
            superResolutionScale = (MTLFXTemporalScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalScalerDescriptor.SupportedInputContentMaxScale(Device));
        }

        if (Capabilities.RayReconstructionSupported)
        {
            rayReconstructionScale = (MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMaxScale(Device));
        }
    }

    public GraphicsContext Context { get; }

    public override UpscalingCapabilities Capabilities { get; }

    public override UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, UpscalingHelper.Scale(mode), superResolutionScale.Min, superResolutionScale.Max);
    }

    public override UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, UpscalingHelper.Scale(mode), rayReconstructionScale.Min, rayReconstructionScale.Max);
    }

    public override SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return new MetalFXSuperResolution(this, desc);
    }

    public override RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return new MetalFXRayReconstruction(this, desc);
    }

    public override FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return new MetalFXFrameGeneration(this, desc);
    }

    public Texture CreateTexture(PixelFormat format, uint width, uint height)
    {
        TextureUsages usages = TextureUsages.Sampled;
        usages |= ZenithHelper.HasDepth(format) ? TextureUsages.DepthStencilAttachment : TextureUsages.ColorAttachment | TextureUsages.Storage;

        return Context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with { Usages = usages });
    }

    public void WarmUp(Action<MTL4CommandBuffer> encode)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandQueue queue = Context.GraphicsQueue.Metal();
        using MTL4CommandAllocator allocator = Device.MakeCommandAllocator();
        using MTL4CommandBuffer commandBuffer = Device.MakeCommandBuffer();
        using MTLSharedEvent @event = Device.MakeSharedEvent();

        commandBuffer.BeginCommandBuffer(allocator);
        encode(commandBuffer);
        commandBuffer.EndCommandBuffer();

        queue.Commit([commandBuffer]);
        queue.SignalEvent(@event, 1);

        @event.Wait(1, ulong.MaxValue);
    }

    protected override void Destroy()
    {
        Compiler.Dispose();
    }

    public static void EncodeBarrier(MTL4CommandBuffer commandBuffer)
    {
        MTL4ComputeCommandEncoder encoder = commandBuffer.MakeComputeCommandEncoder();
        encoder.BarrierAfterStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        encoder.EndEncoding();
    }

    private static UpscalingInputSize GetInputSize(uint outputWidth, uint outputHeight, float scale, float minScale, float maxScale)
    {
        return scale < minScale || scale > maxScale ? default : new((uint)MathF.Floor((outputWidth / scale) + 0.5f), (uint)MathF.Floor((outputHeight / scale) + 0.5f));
    }
}
