using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox.Passes;

internal class FrameGenerationPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private DLSSFrameGeneration? frameGeneration;
    private Texture[] color = [];
    private Texture[] generatedColor = [];
    private Texture[] depth = [];
    private Texture[] motionVectors = [];
    private DLSSFrameGenerationArgs pendingArgs;
    private Texture? pendingFrame;
    private bool history;

    public bool IsGenerated { get; private set; }

    public Texture Color { get; private set; } = null!;

    public Texture GeneratedColor { get; private set; } = null!;

    public void Dispatch(CommandBuffer commandBuffer)
    {
        if (pendingFrame is null)
        {
            return;
        }

        commandBuffer.Transition(pendingFrame, default, TextureLayout.Undefined, TextureLayout.Storage);

        frameGeneration!.Dispatch(commandBuffer, pendingArgs);

        commandBuffer.Transition(pendingFrame, default, TextureLayout.Storage, TextureLayout.Sampled);

        pendingFrame = null;
    }

    protected override void Initialize()
    {
        CreateTextures();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        IsGenerated = false;
        pendingFrame = null;

        if (!args.FrameGeneration)
        {
            history = false;

            return;
        }

        frameGeneration ??= App.Context.CreateDLSSFrameGeneration(Desc());

        Snapshot(commandBuffer, args.Color, color[args.Slot]);
        Snapshot(commandBuffer, args.Depth, depth[args.Slot]);
        Snapshot(commandBuffer, args.MotionVectors, motionVectors[args.Slot]);

        Matrix4x4.Invert(args.ClipToPrevClip, out Matrix4x4 prevClipToClip);

        pendingArgs = new()
        {
            Color = color[args.Slot].DLSSBinding,
            Depth = depth[args.Slot].DLSSBinding,
            MotionVectors = motionVectors[args.Slot].DLSSBinding,
            Output = generatedColor[args.Slot].DLSSBinding,
            JitterOffset = new(2.0f * args.Jitter.X / RenderWidth, -2.0f * args.Jitter.Y / RenderHeight),
            MotionVectorScale = new(-0.5f, 0.5f),
            ViewToClip = args.Projection,
            ClipToView = args.InverseProjection,
            ClipToPrevClip = args.ClipToPrevClip,
            PrevClipToClip = prevClipToClip,
            CameraPosition = args.CameraPosition,
            CameraUp = args.CameraUp,
            CameraRight = args.CameraRight,
            CameraForward = args.CameraForward,
            CameraNear = args.CameraNear,
            CameraFar = args.CameraFar,
            CameraFovAngleVer = args.CameraFovAngleVer,
            CameraAspectRatio = args.CameraAspectRatio,
            Reset = !history
        };
        Color = color[args.Slot];
        GeneratedColor = generatedColor[args.Slot];
        pendingFrame = GeneratedColor;

        IsGenerated = history;
        history = true;
    }

    protected override void ResizeImpl()
    {
        DLSSFrameGenerationDesc desc = Desc();

        if (frameGeneration is not null && !frameGeneration.Desc.Equals(desc))
        {
            DLSSFrameGeneration previous = frameGeneration;
            frameGeneration = App.Context.CreateDLSSFrameGeneration(desc);
            previous.Dispose();
        }

        DestroyTextures();
        CreateTextures();

        history = false;
    }

    protected override void Destroy()
    {
        DestroyTextures();

        frameGeneration?.Dispose();
    }

    private DLSSFrameGenerationDesc Desc()
    {
        return new()
        {
            Format = PixelFormat.B8G8R8A8UNorm,
            InputWidth = RenderWidth,
            InputHeight = RenderHeight,
            OutputWidth = DisplayWidth,
            OutputHeight = DisplayHeight
        };
    }

    private void CreateTextures()
    {
        color = new Texture[App.SlotCount];
        generatedColor = new Texture[App.SlotCount];
        depth = new Texture[App.SlotCount];
        motionVectors = new Texture[App.SlotCount];

        for (int i = 0; i < App.SlotCount; i++)
        {
            color[i] = CreateTexture(DisplayWidth, DisplayHeight, PixelFormat.B8G8R8A8UNorm, TextureUsages.Sampled | TextureUsages.TransferDst);
            generatedColor[i] = CreateTexture(DisplayWidth, DisplayHeight, PixelFormat.B8G8R8A8UNorm);
            depth[i] = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R32Float, TextureUsages.Sampled | TextureUsages.TransferDst);
            motionVectors[i] = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16Float, TextureUsages.Sampled | TextureUsages.TransferDst);
        }
    }

    private void DestroyTextures()
    {
        for (int i = 0; i < color.Length; i++)
        {
            motionVectors[i].Dispose();
            depth[i].Dispose();
            generatedColor[i].Dispose();
            color[i].Dispose();
        }
    }

    private static void Snapshot(CommandBuffer commandBuffer, Texture source, Texture destination)
    {
        commandBuffer.Transition(source, default, TextureLayout.Sampled, TextureLayout.CopySrc);
        commandBuffer.Transition(destination, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(source, default, default, destination, default, default, new()
        {
            Width = source.Desc.Width,
            Height = source.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(destination, default, TextureLayout.CopyDst, TextureLayout.Sampled);
        commandBuffer.Transition(source, default, TextureLayout.CopySrc, TextureLayout.Sampled);
    }
}
