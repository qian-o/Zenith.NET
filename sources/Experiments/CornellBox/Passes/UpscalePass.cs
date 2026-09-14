using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class UpscalePass : Pass
{
    private SpatialUpscaler? spatialUpscaler;
    private TemporalUpscaler? temporalUpscaler;
    private Texture? output;
    private bool resourcesInitialized;

    public Texture Render(CommandBuffer commandBuffer, Texture color, Texture depth, Texture motionVectors, Vector2 jitter, Matrix4x4 clipToPrevClip, float cameraFovAngleHor, bool sameCamera)
    {
        if (output is null)
        {
            return color;
        }

        commandBuffer.Transition(output, default, resourcesInitialized ? TextureLayout.Sampled : TextureLayout.Undefined, TextureLayout.Storage);

        spatialUpscaler?.Dispatch(commandBuffer, new()
        {
            Input = color.SampledHandle,
            Output = output.StorageHandle
        });

        temporalUpscaler?.Dispatch(commandBuffer, new()
        {
            Input = color.SampledHandle,
            OpaqueInput = color.SampledHandle,
            Depth = depth.SampledHandle,
            MotionVectors = motionVectors.SampledHandle,
            Output = output.StorageHandle,
            JitterOffsetX = jitter.X,
            JitterOffsetY = jitter.Y,
            ClipToPrevClip = clipToPrevClip,
            PreExposure = 1.0f,
            CameraFovAngleHor = cameraFovAngleHor,
            MinLerpContribution = 0.0f,
            SameCamera = sameCamera,
            Reset = !resourcesInitialized
        });

        commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);

        resourcesInitialized = true;

        return output;
    }

    public void Resize(uint inputWidth, uint inputHeight, uint outputWidth, uint outputHeight, UpscaleMode mode)
    {
        output?.Dispose();
        output = null;

        temporalUpscaler?.Dispose();
        temporalUpscaler = null;

        spatialUpscaler?.Dispose();
        spatialUpscaler = null;

        resourcesInitialized = false;

        switch (mode)
        {
            case UpscaleMode.Spatial:
                spatialUpscaler = App.Context.CreateSpatialUpscaler(new()
                {
                    InputWidth = inputWidth,
                    InputHeight = inputHeight,
                    OutputWidth = outputWidth,
                    OutputHeight = outputHeight
                });
                break;

            case UpscaleMode.Temporal:
                temporalUpscaler = App.Context.CreateTemporalUpscaler(new()
                {
                    InputWidth = inputWidth,
                    InputHeight = inputHeight,
                    OutputWidth = outputWidth,
                    OutputHeight = outputHeight,
                    Mode = TemporalUpscalerMode.Quality
                });
                break;
        }

        if (mode is not UpscaleMode.None)
        {
            output = CreateTexture(outputWidth, outputHeight, PixelFormat.R16G16B16A16Float);
        }
    }

    protected override void Destroy()
    {
        output?.Dispose();
        temporalUpscaler?.Dispose();
        spatialUpscaler?.Dispose();
    }
}
