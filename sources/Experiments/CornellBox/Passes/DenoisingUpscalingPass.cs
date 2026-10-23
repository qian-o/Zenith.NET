using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox.Passes;

internal class DenoisingUpscalingPass : DisposableObject
{
    private DLSSRayReconstruction? dlss;
    private Texture? output;
    private uint width;
    private uint height;
    private bool hasPendingReset;

    public DenoisingUpscalingPass(uint width, uint height, DenoisingUpscalingMode mode = DenoisingUpscalingMode.Off)
    {
        this.width = width;
        this.height = height;
        Mode = mode;

        CreateResources();
    }

    public DenoisingUpscalingMode Mode { get; }

    public uint InputWidth { get; private set; }

    public uint InputHeight { get; private set; }

    public Vector2 GetJitterOffset(uint frameIndex)
    {
        if (output is null)
        {
            return Vector2.Zero;
        }

        uint sample = frameIndex + 1u;

        return new(Halton(sample, 2u) - 0.5f, Halton(sample, 3u) - 0.5f);
    }

    public Texture Record(CommandBuffer commandBuffer, PathTracingPass inputs, in RenderArgs args)
    {
        switch (App.Technology)
        {
            case RenderingTechnology.DLSS when Mode is not DenoisingUpscalingMode.Off:
                commandBuffer.Transition(output!, default, TextureLayout.Undefined, TextureLayout.Storage);
                dlss!.Dispatch(commandBuffer, new()
                {
                    Color = inputs.Color,
                    Depth = inputs.Depth,
                    MotionVectors = inputs.MotionVectors,
                    DiffuseAlbedo = inputs.DiffuseAlbedo,
                    SpecularAlbedo = inputs.SpecularAlbedo,
                    Normals = inputs.Normals,
                    Roughness = inputs.Roughness,
                    SpecularHitDistance = inputs.SpecularHitDistance,
                    Output = output!,
                    JitterOffset = -args.JitterOffset,
                    MotionVectorScale = Vector2.One,
                    WorldToView = args.View,
                    ViewToClip = args.Projection,
                    PreExposure = 1.0f,
                    Reset = args.Reset || hasPendingReset
                });
                commandBuffer.Transition(output!, default, TextureLayout.Storage, TextureLayout.Sampled);

                hasPendingReset = false;

                return output!;

            case RenderingTechnology.MetalFX:
                break;
        }

        return inputs.Color;
    }

    public void Resize(uint newWidth, uint newHeight)
    {
        if (width == newWidth && height == newHeight)
        {
            return;
        }

        DestroyResources();

        width = newWidth;
        height = newHeight;

        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();
    }

    private void CreateResources()
    {
        InputWidth = width;
        InputHeight = height;

        switch (App.Technology)
        {
            case RenderingTechnology.DLSS when Mode is not DenoisingUpscalingMode.Off:
                DLSSMode mode = Mode switch
                {
                    DenoisingUpscalingMode.Native => DLSSMode.DLAA,
                    DenoisingUpscalingMode.Quality => DLSSMode.Quality,
                    DenoisingUpscalingMode.Balanced => DLSSMode.Balanced,
                    DenoisingUpscalingMode.Performance => DLSSMode.Performance,
                    _ => DLSSMode.UltraPerformance
                };

                CreateDLSS(mode);
                break;

            case RenderingTechnology.MetalFX:
                break;
        }

        hasPendingReset = true;
    }

    private void CreateDLSS(DLSSMode mode)
    {
        DLSSOptimalSettings settings = App.Context.GetDLSSRayReconstructionOptimalSettings(width, height, mode);

        InputWidth = settings.InputWidth;
        InputHeight = settings.InputHeight;

        dlss = App.Context.CreateDLSSRayReconstruction(new()
        {
            InputWidth = InputWidth,
            InputHeight = InputHeight,
            OutputWidth = width,
            OutputHeight = height,
            Mode = mode
        });
        output = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }

    private void DestroyResources()
    {
        output?.Dispose();
        output = null;
        dlss?.Dispose();
        dlss = null;
    }

    private static float Halton(uint index, uint radix)
    {
        float value = 0.0f;
        float weight = 1.0f;

        while (index > 0u)
        {
            weight /= radix;
            value += weight * (index % radix);
            index /= radix;
        }

        return value;
    }
}
