using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class UpscalingPass : DisposableObject
{
    private SuperResolution superResolution = null!;

    public UpscalingPass(UpscalingQuality quality)
    {
        Quality = quality;

        CreateResources();
    }

    public UpscalingQuality Quality { get; }

    public uint RenderWidth => superResolution.Desc.RenderWidth;

    public uint RenderHeight => superResolution.Desc.RenderHeight;

    public Texture Color => superResolution.Output;

    public void Record(CommandBuffer commandBuffer, GBufferPass gBuffer, LightingPass lighting, in FrameContext frame)
    {
        superResolution.Dispatch(commandBuffer, new()
        {
            Color = lighting.Color,
            Depth = gBuffer.Depth,
            MotionVectors = gBuffer.MotionVectors,
            ReactiveMask = lighting.ReactiveMask,
            JitterOffset = frame.JitterOffset,
            Projection = frame.Projection,
            DeltaTimeInSeconds = frame.DeltaTimeInSeconds,
            Reset = frame.ResetLighting
        });
    }

    public void Resize()
    {
        if (Color.Desc.Width == App.PhysicalWidth && Color.Desc.Height == App.PhysicalHeight)
        {
            return;
        }

        DestroyResources();
        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();
    }

    private void CreateResources()
    {
        superResolution = App.Context.CreateSuperResolution(SuperResolutionDesc.Preset(LightingPass.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, Quality));
    }

    private void DestroyResources()
    {
        superResolution?.Dispose();
        superResolution = null!;
    }
}
