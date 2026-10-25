using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox.Passes;

internal class DenoisingUpscalingPass : DisposableObject
{
    private DLSSRayReconstruction? dlss;

    public DenoisingUpscalingPass(RenderDesc desc)
    {
        Resize(desc);
    }

    public RenderDesc Desc { get; private set; }

    public Texture? Color { get; private set; }

    public void Record(CommandBuffer commandBuffer, in RenderArgs args)
    {
        if (dlss is null)
        {
            return;
        }

        commandBuffer.Transition(Color!, default, TextureLayout.Undefined, TextureLayout.Storage);
        dlss.Dispatch(commandBuffer, new()
        {
            Color = args.Input.Color,
            Depth = args.Input.Depth,
            MotionVectors = args.Input.MotionVectors,
            DiffuseAlbedo = args.Input.DiffuseAlbedo,
            SpecularAlbedo = args.Input.SpecularAlbedo,
            Normals = args.Input.Normals,
            Roughness = args.Input.Roughness,
            SpecularHitDistance = args.Input.SpecularHitDistance,
            Output = Color!,
            JitterOffset = -args.JitterOffset,
            MotionVectorScale = Vector2.One,
            WorldToView = args.View,
            ViewToClip = args.Projection,
            PreExposure = 1.0f,
            Reset = args.Reset
        });
        commandBuffer.Transition(Color!, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public void Resize(RenderDesc desc)
    {
        if (Desc.OutputWidth == desc.OutputWidth && Desc.OutputHeight == desc.OutputHeight && Desc.DenoisingUpscaling == desc.DenoisingUpscaling)
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
        if (Desc.DenoisingUpscaling is DenoisingUpscalingMode.Off)
        {
            return;
        }

        DLSSMode mode = Desc.DenoisingUpscaling switch
        {
            DenoisingUpscalingMode.Native => DLSSMode.DLAA,
            DenoisingUpscalingMode.Quality => DLSSMode.Quality,
            DenoisingUpscalingMode.Balanced => DLSSMode.Balanced,
            DenoisingUpscalingMode.Performance => DLSSMode.Performance,
            DenoisingUpscalingMode.UltraPerformance => DLSSMode.UltraPerformance,
            _ => default
        };
        DLSSOptimalSettings settings = App.Context.GetDLSSRayReconstructionOptimalSettings(Desc.OutputWidth, Desc.OutputHeight, mode);

        Desc = Desc with
        {
            InputWidth = settings.InputWidth,
            InputHeight = settings.InputHeight
        };

        dlss = App.Context.CreateDLSSRayReconstruction(new()
        {
            InputWidth = Desc.InputWidth,
            InputHeight = Desc.InputHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight,
            Mode = mode
        });
        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, Desc.OutputWidth, Desc.OutputHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }

    private void DestroyResources()
    {
        Color?.Dispose();
        Color = null;
        dlss?.Dispose();
        dlss = null;
    }
}
