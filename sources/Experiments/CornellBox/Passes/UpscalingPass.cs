using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class UpscalingPass : DisposableObject
{
    private TemporalUpscaler? temporalUpscaler;
    private DenoisingUpscaler? denoisingUpscaler;

    public UpscalingPass(RenderDesc desc)
    {
        Resize(desc);
    }

    public RenderDesc Desc { get; private set; }

    public Texture? Color { get; private set; }

    public void Record(CommandBuffer commandBuffer, in RenderArgs args)
    {
        if (Color is null)
        {
            return;
        }

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        temporalUpscaler?.Dispatch(commandBuffer, new()
        {
            Color = args.Input.Color,
            Depth = args.Input.Depth,
            MotionVectors = args.Input.MotionVectors,
            Output = Color,
            JitterOffset = -args.JitterOffset,
            MotionVectorScale = Vector2.One,
            PreExposure = 1.0f,
            Reset = args.Reset
        });

        denoisingUpscaler?.Dispatch(commandBuffer, new()
        {
            Color = args.Input.Color,
            Depth = args.Input.Depth,
            MotionVectors = args.Input.MotionVectors,
            DiffuseAlbedo = args.Input.DiffuseAlbedo,
            SpecularAlbedo = args.Input.SpecularAlbedo,
            Normals = args.Input.Normals,
            Roughness = args.Input.Roughness,
            SpecularHitDistance = args.Input.SpecularHitDistance,
            Output = Color,
            JitterOffset = -args.JitterOffset,
            MotionVectorScale = Vector2.One,
            WorldToView = args.View,
            ViewToClip = args.Projection,
            PreExposure = 1.0f,
            Reset = args.Reset
        });

        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public void Resize(RenderDesc desc)
    {
        if (Desc.OutputWidth == desc.OutputWidth && Desc.OutputHeight == desc.OutputHeight && Desc.Upscaler == desc.Upscaler && Desc.UpscalingMode == desc.UpscalingMode)
        {
            return;
        }

        DestroyResources();

        Desc = desc;

        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();
    }

    private void CreateResources()
    {
        UpscalingInputSize inputSize = Desc.Upscaler switch
        {
            UpscalerKind.Temporal => App.Context.GetTemporalUpscalerInputSize(Desc.OutputWidth, Desc.OutputHeight, Desc.UpscalingMode),
            UpscalerKind.Denoising => App.Context.GetDenoisingUpscalerInputSize(Desc.OutputWidth, Desc.OutputHeight, Desc.UpscalingMode),
            _ => default
        };

        if (inputSize.Width is 0 || inputSize.Height is 0)
        {
            return;
        }

        Desc = Desc with
        {
            InputWidth = inputSize.Width,
            InputHeight = inputSize.Height
        };

        switch (Desc.Upscaler)
        {
            case UpscalerKind.Temporal:
                temporalUpscaler = App.Context.CreateTemporalUpscaler(new()
                {
                    ColorFormat = PixelFormat.R16G16B16A16Float,
                    DepthFormat = PixelFormat.R32Float,
                    MotionVectorsFormat = PixelFormat.R32G32Float,
                    OutputFormat = PixelFormat.R16G16B16A16Float,
                    InputWidth = Desc.InputWidth,
                    InputHeight = Desc.InputHeight,
                    OutputWidth = Desc.OutputWidth,
                    OutputHeight = Desc.OutputHeight,
                    Flags = UpscalingInputFlags.AutoExposure
                });
                break;

            case UpscalerKind.Denoising:
                denoisingUpscaler = App.Context.CreateDenoisingUpscaler(new()
                {
                    ColorFormat = PixelFormat.R16G16B16A16Float,
                    DepthFormat = PixelFormat.R32Float,
                    MotionVectorsFormat = PixelFormat.R32G32Float,
                    DiffuseAlbedoFormat = PixelFormat.R16G16B16A16Float,
                    SpecularAlbedoFormat = PixelFormat.R16G16B16A16Float,
                    NormalsFormat = PixelFormat.R16G16B16A16Float,
                    RoughnessFormat = PixelFormat.R16Float,
                    SpecularHitDistanceFormat = PixelFormat.R32Float,
                    OutputFormat = PixelFormat.R16G16B16A16Float,
                    InputWidth = Desc.InputWidth,
                    InputHeight = Desc.InputHeight,
                    OutputWidth = Desc.OutputWidth,
                    OutputHeight = Desc.OutputHeight
                });
                break;
        }

        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, Desc.OutputWidth, Desc.OutputHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment
        });
    }

    private void DestroyResources()
    {
        Color?.Dispose();
        Color = null;
        denoisingUpscaler?.Dispose();
        denoisingUpscaler = null;
        temporalUpscaler?.Dispose();
        temporalUpscaler = null;
    }
}
