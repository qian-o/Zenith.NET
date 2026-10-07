using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox.Passes;

internal class RayReconstructionPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private DLSSRayReconstruction? rayReconstruction;
    private Texture output = null!;
    private bool history;

    public Texture Color { get; private set; } = null!;

    protected override void Initialize()
    {
        output = CreateOutput();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        if (args.RayReconstruction is not DLSSMode mode)
        {
            history = false;
            Color = args.Color;

            return;
        }

        DLSSRayReconstructionDesc desc = new()
        {
            InputWidth = RenderWidth,
            InputHeight = RenderHeight,
            OutputWidth = DisplayWidth,
            OutputHeight = DisplayHeight,
            Mode = mode,
            IsRoughnessPacked = true
        };

        if (rayReconstruction is null || !rayReconstruction.Desc.Equals(desc))
        {
            DLSSRayReconstruction? previous = rayReconstruction;
            rayReconstruction = App.Context.CreateDLSSRayReconstruction(desc);
            previous?.Dispose();

            history = false;
        }

        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);

        rayReconstruction.Dispatch(commandBuffer, new()
        {
            Input = args.Color.DLSSBinding,
            DiffuseAlbedo = args.DiffuseAlbedo.DLSSBinding,
            SpecularAlbedo = args.SpecularAlbedo.DLSSBinding,
            Normals = args.Normal.DLSSBinding,
            Depth = args.Depth.DLSSBinding,
            MotionVectors = args.MotionVectors.DLSSBinding,
            SpecularHitDistance = args.SpecularHitDistance.DLSSBinding,
            Output = output.DLSSBinding,
            JitterOffsetX = args.Jitter.X,
            JitterOffsetY = args.Jitter.Y,
            MotionVectorScaleX = -0.5f * RenderWidth,
            MotionVectorScaleY = 0.5f * RenderHeight,
            WorldToView = args.View,
            ViewToClip = args.Projection,
            Reset = !history
        });

        commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);

        history = true;
        Color = output;
    }

    protected override void ResizeImpl()
    {
        output.Dispose();
        output = CreateOutput();

        history = false;
    }

    protected override void Destroy()
    {
        output?.Dispose();
        rayReconstruction?.Dispose();
    }

    private Texture CreateOutput()
    {
        return CreateTexture(DisplayWidth, DisplayHeight, PixelFormat.R16G16B16A16Float, TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferDst);
    }
}
