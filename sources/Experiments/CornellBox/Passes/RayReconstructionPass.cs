using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox.Passes;

internal class RayReconstructionPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private DLSSRayReconstruction? rayReconstruction;
    private Texture output = null!;
    private bool hasHistory;

    public Texture Color { get; private set; } = null!;

    protected override void Initialize()
    {
        output = CreateOutput();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        if (args.RayReconstruction is not DLSSMode mode)
        {
            hasHistory = false;
            Color = args.Color;

            return;
        }

        if (rayReconstruction is null || rayReconstruction.Desc.Mode != mode)
        {
            rayReconstruction?.Dispose();
            rayReconstruction = null;
            rayReconstruction = App.Context.CreateDLSSRayReconstruction(new()
            {
                InputWidth = RenderWidth,
                InputHeight = RenderHeight,
                OutputWidth = DisplayWidth,
                OutputHeight = DisplayHeight,
                Mode = mode,
                Flags = DLSSRayReconstructionFlags.None
            });

            hasHistory = false;
        }

        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);

        rayReconstruction.Dispatch(commandBuffer, new()
        {
            Color = args.Color,
            Depth = args.Depth,
            MotionVectors = args.MotionVectors,
            DiffuseAlbedo = args.DiffuseAlbedo,
            SpecularAlbedo = args.SpecularAlbedo,
            Normals = args.Normal,
            Roughness = args.Roughness,
            SpecularHitDistance = args.SpecularHitDistance,
            Output = output,
            JitterOffset = args.Jitter,
            MotionVectorScale = new(-0.5f * RenderWidth, 0.5f * RenderHeight),
            WorldToView = args.View,
            ViewToClip = args.Projection,
            Reset = !hasHistory || args.ResetHistory
        });

        commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);

        hasHistory = true;
        Color = output;
    }

    protected override void ResizeImpl()
    {
        rayReconstruction?.Dispose();
        rayReconstruction = null;
        output.Dispose();
        output = CreateOutput();

        hasHistory = false;
    }

    protected override void Destroy()
    {
        rayReconstruction?.Dispose();
        output?.Dispose();
    }

    private Texture CreateOutput()
    {
        return CreateTexture(DisplayWidth, DisplayHeight, PixelFormat.R16G16B16A16Float, TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferDst);
    }
}
