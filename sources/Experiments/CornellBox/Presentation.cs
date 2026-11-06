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

    public Presentation(Surface surface, UpscalingInputSize inputSize)
    {
        CreateSwapChain(surface, inputSize, useFrameGeneration: false);
    }

    public void Configure(UpscalingInputSize renderSize, bool useFrameGeneration)
    {
        Surface surface = frameGenerationSwapChain?.Desc.Surface ?? swapChain!.Desc.Surface;
        bool hasSwapChainChanges = (frameGenerationSwapChain is not null) != useFrameGeneration;

        if (frameGenerationSwapChain is not null)
        {
            FrameGenerationSwapChainDesc desc = frameGenerationSwapChain.Desc;
            hasSwapChainChanges |= desc.AuxiliaryWidth != renderSize.Width || desc.AuxiliaryHeight != renderSize.Height;
        }

        if (!hasSwapChainChanges && surface.Width == App.PhysicalWidth && surface.Height == App.PhysicalHeight)
        {
            return;
        }

        if (hasSwapChainChanges)
        {
            surface = surface with
            {
                Width = App.PhysicalWidth,
                Height = App.PhysicalHeight
            };

            DestroySwapChain();
            CreateSwapChain(surface, renderSize, useFrameGeneration);

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

    public void Present(CommandBuffer commandBuffer, in RenderedFrame frame)
    {
        if (frameGenerationSwapChain is not null)
        {
            CopyTexture(commandBuffer, frame.Depth, frameGenerationSwapChain.Depth);
            CopyTexture(commandBuffer, frame.MotionVectors, frameGenerationSwapChain.MotionVectors);
            CopyTexture(commandBuffer, frame.Color, frameGenerationSwapChain.Hudless!);
        }

        ImGui.GetBackgroundDrawList().AddImage(frame.Color.ImGuiBinding, Vector2.Zero, new(App.LogicalWidth, App.LogicalHeight));

        Texture color = frameGenerationSwapChain?.Color ?? swapChain!.Drawable;
        commandBuffer.Transition(color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        App.Context.EndImGuiFrame(commandBuffer, ColorAttachment.Clear(color, default));
        commandBuffer.Transition(color, default, TextureLayout.ColorAttachment, TextureLayout.Present);
        commandBuffer.Submit().Wait();

        if (frameGenerationSwapChain is not null)
        {
            frameGenerationSwapChain.Present(new()
            {
                JitterOffset = frame.JitterOffset,
                WorldToView = frame.WorldToView,
                ViewToClip = frame.ViewToClip,
                DeltaTimeInSeconds = frame.DeltaTimeInSeconds,
                Reset = frame.Reset
            });
        }
        else
        {
            swapChain!.Present();
        }
    }

    protected override void Destroy()
    {
        DestroySwapChain();
    }

    private void CreateSwapChain(Surface surface, UpscalingInputSize inputSize, bool useFrameGeneration)
    {
        if (useFrameGeneration)
        {
            frameGenerationSwapChain = App.Context.CreateFrameGenerationSwapChain(new()
            {
                Surface = surface,
                ColorFormat = App.ColorFormat,
                DepthFormat = PathTracingOutput.DepthFormat,
                MotionVectorsFormat = PathTracingOutput.MotionVectorsFormat,
                HudlessFormat = App.ColorFormat,
                AuxiliaryWidth = inputSize.Width,
                AuxiliaryHeight = inputSize.Height
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
        frameGenerationSwapChain?.Dispose();
        frameGenerationSwapChain = null;
        swapChain?.Dispose();
        swapChain = null;
    }

    private static void CopyTexture(CommandBuffer commandBuffer, Texture src, Texture dst)
    {
        commandBuffer.Transition(src, default, TextureLayout.Sampled, TextureLayout.CopySrc);
        commandBuffer.Transition(dst, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(src, default, default, dst, default, default, new()
        {
            Width = src.Desc.Width,
            Height = src.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(dst, default, TextureLayout.CopyDst, TextureLayout.Present);
        commandBuffer.Transition(src, default, TextureLayout.CopySrc, TextureLayout.Sampled);
    }
}
