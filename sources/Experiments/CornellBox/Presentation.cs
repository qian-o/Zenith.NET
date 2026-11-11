using System.Numerics;
using CornellBox.Models;
using Hexa.NET.ImGui;
using Zenith.NET;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox;

internal class Presentation : DisposableObject
{
    private SwapChain? swapChain;
    private FrameGenerationSwapChain? frameGenerationSwapChain;

    public Presentation(Surface surface, bool useFrameGeneration)
    {
        CreateSwapChain(surface, useFrameGeneration);
    }

    public void Configure(bool useFrameGeneration)
    {
        Surface surface = frameGenerationSwapChain?.Desc.Surface ?? swapChain!.Desc.Surface;

        if ((frameGenerationSwapChain is not null) != useFrameGeneration)
        {
            surface = surface with
            {
                Width = App.PhysicalWidth,
                Height = App.PhysicalHeight
            };

            DestroySwapChain();
            CreateSwapChain(surface, useFrameGeneration);

            return;
        }

        if (surface.Width == App.PhysicalWidth && surface.Height == App.PhysicalHeight)
        {
            return;
        }

        if (frameGenerationSwapChain is not null)
        {
            frameGenerationSwapChain.Resize(App.PhysicalWidth, App.PhysicalHeight);
        }
        else
        {
            swapChain!.Resize(App.PhysicalWidth, App.PhysicalHeight);
        }
    }

    public void WaitForPendingWork()
    {
        App.Context.GraphicsQueue.Timeline.Signal().Wait();
    }

    public void Present(CommandBuffer commandBuffer, RenderedFrame frame)
    {
        ImGui.GetBackgroundDrawList().AddImage(frame.Color.ImGuiBinding, Vector2.Zero, new(App.LogicalWidth, App.LogicalHeight));

        Texture drawable = frameGenerationSwapChain?.Drawable ?? swapChain!.Drawable;
        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        App.Context.EndImGuiFrame(commandBuffer, ColorAttachment.Clear(drawable, default));
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

        if (frameGenerationSwapChain is not null)
        {
            frameGenerationSwapChain.Present(commandBuffer, new()
            {
                Depth = frame.Depth,
                MotionVectors = frame.MotionVectors,
                Hudless = frame.Color,
                JitterOffset = frame.JitterOffset,
                View = frame.View,
                Projection = frame.Projection,
                DeltaTimeInSeconds = frame.DeltaTimeInSeconds,
                Reset = frame.Reset
            });
        }
        else
        {
            commandBuffer.Submit().Wait();
            swapChain!.Present();
        }
    }

    protected override void Destroy()
    {
        DestroySwapChain();
    }

    private void CreateSwapChain(Surface surface, bool useFrameGeneration)
    {
        if (useFrameGeneration)
        {
            frameGenerationSwapChain = App.Context.CreateFrameGenerationSwapChain(new()
            {
                Surface = surface,
                Format = App.ColorFormat
            });
        }
        else
        {
            swapChain = App.Context.CreateSwapChain(new()
            {
                Surface = surface,
                Format = App.ColorFormat
            });
        }
    }

    private void DestroySwapChain()
    {
        WaitForPendingWork();

        frameGenerationSwapChain?.Dispose();
        frameGenerationSwapChain = null;
        swapChain?.Dispose();
        swapChain = null;
    }
}
