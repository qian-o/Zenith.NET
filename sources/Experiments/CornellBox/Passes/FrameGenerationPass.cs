using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class FrameGenerationPass : DisposableObject
{
    private FrameGenerator? frameGenerator;
    private Texture[] colors = [];

    public FrameGenerationPass(RenderDesc desc)
    {
        Resize(desc);
    }

    public ReadOnlySpan<Texture> Colors => colors;

    public void Record(CommandBuffer commandBuffer, in RenderArgs args)
    {
        if (frameGenerator is null)
        {
            return;
        }

        commandBuffer.Transition(colors[0], default, TextureLayout.Undefined, TextureLayout.Storage);
        frameGenerator.Dispatch(commandBuffer, new()
        {
            Color = args.Color,
            Depth = args.Input.Depth,
            MotionVectors = args.Input.MotionVectors,
            Output = colors[0],
            DeltaTimeInSeconds = (float)args.DeltaSeconds,
            JitterOffset = -args.JitterOffset,
            MotionVectorScale = Vector2.One,
            WorldToView = args.View,
            ViewToClip = args.Projection,
            Reset = args.Reset
        });
        commandBuffer.Transition(colors[0], default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public void Resize(RenderDesc desc)
    {
        if (colors.Length == (int)desc.FrameGeneration && (frameGenerator is null || IsSameSize(frameGenerator.Desc, desc)))
        {
            return;
        }

        DestroyResources();
        CreateResources(desc);
    }

    protected override void Destroy()
    {
        DestroyResources();
    }

    private void CreateResources(RenderDesc desc)
    {
        if (desc.FrameGeneration is FrameGenerationMode.X1)
        {
            return;
        }

        frameGenerator = App.Context.CreateFrameGenerator(new()
        {
            ColorFormat = PixelFormat.R8G8B8A8UNorm,
            DepthFormat = PixelFormat.R32Float,
            MotionVectorsFormat = PixelFormat.R32G32Float,
            InputWidth = desc.InputWidth,
            InputHeight = desc.InputHeight,
            OutputWidth = desc.OutputWidth,
            OutputHeight = desc.OutputHeight
        });

        colors = [App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, desc.OutputWidth, desc.OutputHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment
        })];
    }

    private void DestroyResources()
    {
        for (int i = colors.Length - 1; i >= 0; i--)
        {
            colors[i]?.Dispose();
        }
        colors = [];

        frameGenerator?.Dispose();
        frameGenerator = null;
    }

    private static bool IsSameSize(FrameGeneratorDesc frameGeneratorDesc, RenderDesc desc)
    {
        return frameGeneratorDesc.InputWidth == desc.InputWidth && frameGeneratorDesc.InputHeight == desc.InputHeight && frameGeneratorDesc.OutputWidth == desc.OutputWidth && frameGeneratorDesc.OutputHeight == desc.OutputHeight;
    }
}
