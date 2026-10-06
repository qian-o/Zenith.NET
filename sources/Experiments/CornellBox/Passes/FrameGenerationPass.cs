using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox.Passes;

// Frame generation runs on the compute queue, overlapping the next frame's rendering.
internal class FrameGenerationPass(uint width, uint height) : Pass(width, height)
{
    private DLSSFrameGeneration frameGeneration = null!;
    private Texture[] hudless = [];
    private Texture[] depth = [];
    private Texture[] motionVectors = [];
    private DLSSFrameGenerationArgs pendingArgs;
    private Texture? pendingBackBuffer;
    private Texture? pendingFrame;
    private bool history;

    public bool IsGenerated { get; private set; }

    public TimelineValue Dispatch(TimelineValue rendered)
    {
        if (pendingBackBuffer is null || pendingFrame is null)
        {
            return rendered;
        }

        CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

        commandBuffer.Transition(pendingFrame, default, TextureLayout.Undefined, TextureLayout.Storage);

        frameGeneration.Dispatch(commandBuffer, pendingArgs);

        commandBuffer.Transition(pendingFrame, default, TextureLayout.Storage, TextureLayout.CopySrc);
        commandBuffer.Transition(pendingBackBuffer, default, TextureLayout.Sampled, TextureLayout.CopySrc);

        pendingBackBuffer = null;
        pendingFrame = null;

        return commandBuffer.Submit(rendered);
    }

    protected override void Initialize()
    {
        // A live DLSS object keeps NGX initialized, so toggling Ray Reconstruction stays fast.
        frameGeneration = App.Context.CreateDLSSFrameGeneration(Desc());

        CreateTextures();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        IsGenerated = false;

        if (!args.FrameGeneration)
        {
            commandBuffer.Transition(args.BackBuffer, default, TextureLayout.Sampled, TextureLayout.CopySrc);

            history = false;

            return;
        }

        // The next frame overwrites these inputs while frame generation is still running.
        Snapshot(commandBuffer, args.Color, hudless[args.Slot]);
        Snapshot(commandBuffer, args.Depth, depth[args.Slot]);
        Snapshot(commandBuffer, args.MotionVectors, motionVectors[args.Slot]);

        pendingArgs = new()
        {
            Color = args.BackBuffer.DLSSBinding,
            HudlessColor = hudless[args.Slot].DLSSBinding,
            UI = args.UI.DLSSBinding,
            Depth = depth[args.Slot].DLSSBinding,
            MotionVectors = motionVectors[args.Slot].DLSSBinding,
            Output = args.GeneratedFrame.DLSSBinding,
            JitterOffsetX = 2.0f * args.Jitter.X / Width,
            JitterOffsetY = -2.0f * args.Jitter.Y / Height,
            MotionVectorScaleX = -0.5f,
            MotionVectorScaleY = 0.5f,
            ViewToClip = args.Projection,
            ClipToPrevClip = args.ClipToPrevClip,
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
        pendingBackBuffer = args.BackBuffer;
        pendingFrame = args.GeneratedFrame;

        // Without history the output is only a copy of the real frame.
        IsGenerated = history;
        history = true;
    }

    protected override void ResizeImpl()
    {
        DLSSFrameGenerationDesc desc = Desc();

        if (!frameGeneration.Desc.Equals(desc))
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

    private DLSSFrameGenerationDesc Desc()
    {
        return new()
        {
            Format = PixelFormat.B8G8R8A8UNorm,
            InputWidth = Width,
            InputHeight = Height,
            OutputWidth = Width,
            OutputHeight = Height,
            IsUIRecompositionEnabled = true
        };
    }

    private void CreateTextures()
    {
        hudless = new Texture[App.SlotCount];
        depth = new Texture[App.SlotCount];
        motionVectors = new Texture[App.SlotCount];

        for (int i = 0; i < App.SlotCount; i++)
        {
            hudless[i] = CreateTexture(Width, Height, PixelFormat.B8G8R8A8UNorm, TextureUsages.Sampled | TextureUsages.TransferDst);
            depth[i] = CreateTexture(Width, Height, PixelFormat.R32Float, TextureUsages.Sampled | TextureUsages.TransferDst);
            motionVectors[i] = CreateTexture(Width, Height, PixelFormat.R16G16Float, TextureUsages.Sampled | TextureUsages.TransferDst);
        }
    }

    private void DestroyTextures()
    {
        for (int i = 0; i < hudless.Length; i++)
        {
            motionVectors[i].Dispose();
            depth[i].Dispose();
            hudless[i].Dispose();
        }
    }
}
