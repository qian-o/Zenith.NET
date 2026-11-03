using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class FrameGenerationPass : DisposableObject
{
    private readonly FrameGenerator frameGenerator;

    public FrameGenerationPass(uint inputWidth, uint inputHeight, uint outputWidth, uint outputHeight)
    {
        frameGenerator = App.Context.CreateFrameGenerator(new()
        {
            ColorFormat = PixelFormat.R8G8B8A8UNorm,
            DepthFormat = PixelFormat.R32Float,
            MotionVectorsFormat = PixelFormat.R32G32Float,
            InputWidth = inputWidth,
            InputHeight = inputHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        });

        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, outputWidth, outputHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment
        });
    }

    public Texture Color { get; }

    public void Record(CommandBuffer commandBuffer, Texture color, PathTracingOutput input, CameraHandler camera, Vector2 jitterOffset, double deltaSeconds, bool reset)
    {
        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);
        frameGenerator.Dispatch(commandBuffer, new()
        {
            Color = color,
            Depth = input.Depth,
            MotionVectors = input.MotionVectors,
            Output = Color,
            DeltaTimeInSeconds = (float)deltaSeconds,
            JitterOffset = -jitterOffset,
            MotionVectorScale = Vector2.One,
            WorldToView = camera.View,
            ViewToClip = camera.Projection,
            Reset = reset
        });
        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        Color?.Dispose();
        frameGenerator?.Dispose();
    }
}
