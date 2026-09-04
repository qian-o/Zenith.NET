using Zenith.NET.Extensions.Upscaling.Passes;

namespace Zenith.NET.Extensions.Upscaling;

public class TemporalUpscaler : DisposableObject
{
    private readonly Sgsr2ConvertPass convertPass;
    private readonly Sgsr2ActivatePass? activatePass;
    private readonly Sgsr2UpscalePass upscalePass;
    private readonly Texture yCoCg;
    private readonly Texture? motionDepthAlpha;
    private readonly Texture motionDepthClipAlpha;
    private readonly Texture? luma0;
    private readonly Texture? luma1;
    private readonly Texture history0;
    private readonly Texture history1;

    private bool resourcesInitialized;
    private bool historySelect;

    internal TemporalUpscaler(GraphicsContext context, TemporalUpscalerDesc desc)
    {
        convertPass = new(context, desc.Mode);
        activatePass = desc.Mode is TemporalUpscalerMode.Quality ? new(context) : null;
        upscalePass = new(context, desc.Mode);

        yCoCg = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R32UInt,
            Width = desc.InputWidth,
            Height = desc.InputHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
        motionDepthAlpha = desc.Mode is TemporalUpscalerMode.Quality
            ? context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R16G16B16A16Float,
                Width = desc.InputWidth,
                Height = desc.InputHeight,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Usages = TextureUsages.Sampled | TextureUsages.Storage
            })
            : null;
        motionDepthClipAlpha = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = desc.InputWidth,
            Height = desc.InputHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
        luma0 = desc.Mode is TemporalUpscalerMode.Quality
            ? context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R32UInt,
                Width = desc.InputWidth,
                Height = desc.InputHeight,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Usages = TextureUsages.Sampled | TextureUsages.Storage
            })
            : null;
        luma1 = desc.Mode is TemporalUpscalerMode.Quality
            ? context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R32UInt,
                Width = desc.InputWidth,
                Height = desc.InputHeight,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Usages = TextureUsages.Sampled | TextureUsages.Storage
            })
            : null;

        history0 = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = desc.OutputWidth,
            Height = desc.OutputHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
        history1 = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = desc.OutputWidth,
            Height = desc.OutputHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        Desc = desc;
    }

    public TemporalUpscalerDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, TemporalUpscalerArgs args)
    {
        commandBuffer.BeginDebugEvent("TemporalUpscaler");

        Texture historyRead = historySelect ? history1 : history0;
        Texture historyWrite = historySelect ? history0 : history1;
        Texture? lumaRead = historySelect ? luma1 : luma0;
        Texture? lumaWrite = historySelect ? luma0 : luma1;

        if (!resourcesInitialized)
        {
            commandBuffer.Transition(yCoCg, default, TextureLayout.Undefined, TextureLayout.Storage);

            if (motionDepthAlpha is not null)
            {
                commandBuffer.Transition(motionDepthAlpha, default, TextureLayout.Undefined, TextureLayout.Storage);
            }

            commandBuffer.Transition(motionDepthClipAlpha, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.Transition(historyRead, default, TextureLayout.Undefined, TextureLayout.Sampled);
            commandBuffer.Transition(historyWrite, default, TextureLayout.Undefined, TextureLayout.Storage);

            if (lumaRead is not null && lumaWrite is not null)
            {
                commandBuffer.Transition(lumaRead, default, TextureLayout.Undefined, TextureLayout.Sampled);
                commandBuffer.Transition(lumaWrite, default, TextureLayout.Undefined, TextureLayout.Storage);
            }

            resourcesInitialized = true;
        }
        else
        {
            commandBuffer.Transition(yCoCg, default, TextureLayout.Sampled, TextureLayout.Storage);

            if (motionDepthAlpha is not null)
            {
                commandBuffer.Transition(motionDepthAlpha, default, TextureLayout.Sampled, TextureLayout.Storage);
            }

            commandBuffer.Transition(motionDepthClipAlpha, default, TextureLayout.Sampled, TextureLayout.Storage);
            commandBuffer.Transition(historyRead, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(historyWrite, default, TextureLayout.Sampled, TextureLayout.Storage);

            if (lumaWrite is not null)
            {
                commandBuffer.Transition(lumaWrite, default, TextureLayout.Sampled, TextureLayout.Storage);
            }
        }

        ResourceHandle convertMotion = motionDepthAlpha is not null ? motionDepthAlpha.StorageHandle : motionDepthClipAlpha.StorageHandle;
        convertPass.Record(commandBuffer, Desc, args, yCoCg.StorageHandle, convertMotion);

        commandBuffer.Transition(yCoCg, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (activatePass is not null && motionDepthAlpha is not null && lumaRead is not null && lumaWrite is not null)
        {
            commandBuffer.Transition(motionDepthAlpha, default, TextureLayout.Storage, TextureLayout.Sampled);

            activatePass.Record(commandBuffer,
                                Desc,
                                args,
                                lumaRead.SampledHandle,
                                motionDepthAlpha.SampledHandle,
                                yCoCg.SampledHandle,
                                motionDepthClipAlpha.StorageHandle,
                                lumaWrite.StorageHandle);

            commandBuffer.Transition(motionDepthClipAlpha, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(lumaWrite, default, TextureLayout.Storage, TextureLayout.Sampled);
        }
        else
        {
            commandBuffer.Transition(motionDepthClipAlpha, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        upscalePass.Record(commandBuffer,
                           Desc,
                           args,
                           historyRead.SampledHandle,
                           motionDepthClipAlpha.SampledHandle,
                           yCoCg.SampledHandle,
                           args.Output,
                           historyWrite.StorageHandle);

        historySelect = !historySelect;

        commandBuffer.EndDebugEvent();
    }

    protected override void Destroy()
    {
        history1.Dispose();
        history0.Dispose();
        luma1?.Dispose();
        luma0?.Dispose();
        motionDepthClipAlpha.Dispose();
        motionDepthAlpha?.Dispose();
        yCoCg.Dispose();
        upscalePass.Dispose();
        activatePass?.Dispose();
        convertPass.Dispose();
    }
}
