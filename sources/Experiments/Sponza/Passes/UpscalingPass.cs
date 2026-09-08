using System.Runtime.InteropServices;
using Sponza.Helpers;
using Sponza.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Passes;

internal class UpscalingPass : IDisposable
{
    private readonly GraphicsContext context;

    private Buffer bilinearConstants = null!;
    private Sampler sampler = null!;
    private ComputePipeline bilinearPipeline = null!;
    private SpatialUpscaler spatialUpscaler = null!;
    private TemporalUpscaler temporalUpscaler = null!;
    private Texture temporalHdr = null!;
    private TextureLayout temporalLayout;

    public UpscalingPass(GraphicsContext context)
    {
        this.context = context;
    }

    public void Configure(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight, UpscalingMode mode)
    {
        bool bilinearRequired = renderWidth != displayWidth || renderHeight != displayHeight;
        if (mode is UpscalingMode.None && spatialUpscaler is null && temporalUpscaler is null && (bilinearPipeline is not null) == bilinearRequired)
        {
            return;
        }

        if (mode is UpscalingMode.Spatial && spatialUpscaler is not null && spatialUpscaler.Desc.InputWidth == renderWidth && spatialUpscaler.Desc.InputHeight == renderHeight && spatialUpscaler.Desc.OutputWidth == displayWidth && spatialUpscaler.Desc.OutputHeight == displayHeight)
        {
            return;
        }

        if (mode is UpscalingMode.Temporal && temporalUpscaler is not null && temporalUpscaler.Desc.InputWidth == renderWidth && temporalUpscaler.Desc.InputHeight == renderHeight && temporalUpscaler.Desc.OutputWidth == displayWidth && temporalUpscaler.Desc.OutputHeight == displayHeight)
        {
            return;
        }

        DisposeResources();

        switch (mode)
        {
            case UpscalingMode.None:
                {
                    if (bilinearRequired)
                    {
                        bilinearConstants = GraphicsHelper.CreateConstantBuffer<BilinearConstants>(context);
                        sampler = context.CreateSampler(SamplerDesc.LinearClamp());
                        bilinearPipeline = GraphicsHelper.CreateComputePipeline(context, "Bilinear.slang", "Main");
                    }
                }
                break;

            case UpscalingMode.Spatial:
                {
                    spatialUpscaler = context.CreateSpatialUpscaler(new()
                    {
                        InputWidth = renderWidth,
                        InputHeight = renderHeight,
                        OutputWidth = displayWidth,
                        OutputHeight = displayHeight
                    });
                }
                break;

            case UpscalingMode.Temporal:
                {
                    temporalUpscaler = context.CreateTemporalUpscaler(new()
                    {
                        InputWidth = renderWidth,
                        InputHeight = renderHeight,
                        OutputWidth = displayWidth,
                        OutputHeight = displayHeight,
                        Mode = TemporalUpscalerMode.Quality
                    });
                    temporalHdr = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, displayWidth, displayHeight, TextureUsages.Sampled | TextureUsages.Storage);
                }
                break;
        }
    }

    public void RecordBilinear(CommandBuffer commandBuffer, UpscalingPassArgs args)
    {
        GraphicsHelper.Upload<BilinearConstants>(bilinearConstants, new()
        {
            Input = args.Input.SampledHandle,
            Output = args.Target.StorageHandle,
            Sampler = sampler.Handle,
            Width = args.Target.Desc.Width,
            Height = args.Target.Desc.Height
        });

        commandBuffer.Transition(args.Target, default, args.TargetLayout, TextureLayout.Storage);
        commandBuffer.SetPipeline(bilinearPipeline);
        commandBuffer.SetConstantBuffer(bilinearConstants, 0);
        commandBuffer.Dispatch((args.Target.Desc.Width + 7) / 8, (args.Target.Desc.Height + 7) / 8, 1);
        commandBuffer.Transition(args.Target, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public void RecordSpatial(CommandBuffer commandBuffer, UpscalingPassArgs args)
    {
        commandBuffer.Transition(args.Target, default, args.TargetLayout, TextureLayout.Storage);
        spatialUpscaler.Dispatch(commandBuffer, new()
        {
            Input = args.Input.SampledHandle,
            Output = args.Target.StorageHandle
        });
        commandBuffer.Transition(args.Target, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public Texture RecordTemporal(CommandBuffer commandBuffer, TemporalUpscalingPassArgs args)
    {
        commandBuffer.Transition(temporalHdr, default, temporalLayout, TextureLayout.Storage);
        temporalUpscaler.Dispatch(commandBuffer, new()
        {
            Input = args.HdrColor.SampledHandle,
            OpaqueInput = args.HdrColor.SampledHandle,
            Depth = args.DeviceDepth.SampledHandle,
            MotionVectors = args.EncodedMotion.SampledHandle,
            Output = temporalHdr.StorageHandle,
            JitterOffsetX = args.Frame.JitterInPixels.X,
            JitterOffsetY = args.Frame.JitterInPixels.Y,
            ClipToPrevClip = args.Frame.ClipToPrevClip,
            PreExposure = 1.0f,
            CameraFovAngleHor = args.Frame.HorizontalHalfFovTan,
            MinLerpContribution = 0.0f,
            SameCamera = args.Frame.SameCamera,
            Reset = args.Frame.Reset
        });
        commandBuffer.Transition(temporalHdr, default, TextureLayout.Storage, TextureLayout.Sampled);
        temporalLayout = TextureLayout.Sampled;

        return temporalHdr;
    }

    public void Dispose()
    {
        DisposeResources();
    }

    private void DisposeResources()
    {
        temporalHdr?.Dispose();
        temporalUpscaler?.Dispose();
        spatialUpscaler?.Dispose();
        bilinearPipeline?.Dispose();
        sampler?.Dispose();
        bilinearConstants?.Dispose();

        temporalHdr = null!;
        temporalUpscaler = null!;
        spatialUpscaler = null!;
        bilinearPipeline = null!;
        sampler = null!;
        bilinearConstants = null!;
        temporalLayout = TextureLayout.Undefined;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct BilinearConstants
{
    [FieldOffset(0)]
    public ResourceHandle Input;

    [FieldOffset(8)]
    public ResourceHandle Output;

    [FieldOffset(16)]
    public ResourceHandle Sampler;

    [FieldOffset(24)]
    public uint Width;

    [FieldOffset(28)]
    public uint Height;
}
