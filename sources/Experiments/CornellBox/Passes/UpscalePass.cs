using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class UpscalePass : Pass
{
    private SpatialUpscaler? spatialUpscaler;
    private TemporalUpscaler? temporalUpscaler;
    private Texture output = null!;
    private bool resourcesInitialized;
    private bool temporalHistory;

    public Texture Color { get; private set; } = null!;

    public override void Record(CommandBuffer commandBuffer, in PassArgs args)
    {
        if (args.UpscaleMode is UpscaleMode.None)
        {
            temporalHistory = false;
            Color = args.ResolvedColor;
            return;
        }

        commandBuffer.Transition(output, default, resourcesInitialized ? TextureLayout.Sampled : TextureLayout.Undefined, TextureLayout.Storage);

        switch (args.UpscaleMode)
        {
            case UpscaleMode.Spatial:
                RecordSpatial(commandBuffer, in args);
                break;

            case UpscaleMode.Temporal:
                RecordTemporal(commandBuffer, in args);
                break;
        }

        commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);

        resourcesInitialized = true;
        temporalHistory = args.UpscaleMode is UpscaleMode.Temporal;
        Color = output;
    }

    public override void Resize(uint width, uint height)
    {
        output?.Dispose();
        output = CreateTexture(width, height, PixelFormat.R16G16B16A16Float);

        temporalUpscaler?.Dispose();
        temporalUpscaler = null;

        spatialUpscaler?.Dispose();
        spatialUpscaler = null;

        resourcesInitialized = false;
        temporalHistory = false;
    }

    protected override void Destroy()
    {
        output?.Dispose();
        temporalUpscaler?.Dispose();
        spatialUpscaler?.Dispose();
    }

    private void RecordSpatial(CommandBuffer commandBuffer, in PassArgs args)
    {
        spatialUpscaler ??= App.Context.CreateSpatialUpscaler(new()
        {
            InputWidth = args.ResolvedColor.Desc.Width,
            InputHeight = args.ResolvedColor.Desc.Height,
            OutputWidth = output.Desc.Width,
            OutputHeight = output.Desc.Height
        });

        spatialUpscaler.Dispatch(commandBuffer, new()
        {
            Input = args.ResolvedColor.SampledHandle,
            Output = output.StorageHandle
        });
    }

    private void RecordTemporal(CommandBuffer commandBuffer, in PassArgs args)
    {
        temporalUpscaler ??= App.Context.CreateTemporalUpscaler(new()
        {
            InputWidth = args.Color.Desc.Width,
            InputHeight = args.Color.Desc.Height,
            OutputWidth = output.Desc.Width,
            OutputHeight = output.Desc.Height,
            Mode = TemporalUpscalerMode.Quality
        });

        temporalUpscaler.Dispatch(commandBuffer, new()
        {
            Input = args.Color.SampledHandle,
            OpaqueInput = args.Color.SampledHandle,
            Depth = args.Depth.SampledHandle,
            MotionVectors = args.MotionVectors.SampledHandle,
            Output = output.StorageHandle,
            JitterOffsetX = args.Jitter.X,
            JitterOffsetY = args.Jitter.Y,
            ClipToPrevClip = args.ClipToPrevClip,
            PreExposure = 1.0f,
            CameraFovAngleHor = args.CameraFovAngleHor,
            MinLerpContribution = 0.0f,
            SameCamera = args.SameCamera,
            Reset = !temporalHistory
        });
    }
}
