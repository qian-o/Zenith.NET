using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class FrameGenerationPass : DisposableObject
{
    private DLSSFrameGeneration? dlss;
    private Buffer? disableInterpolation;
    private Texture[] colors = [];

    public FrameGenerationPass(RenderDesc desc)
    {
        Resize(desc);
    }

    public ReadOnlySpan<Texture> Colors => colors;

    public Buffer? Readback { get; private set; }

    public void Record(CommandBuffer commandBuffer, in RenderArgs args)
    {
        if (dlss is null)
        {
            return;
        }

        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);
        Matrix4x4.Invert(args.PreviousView, out Matrix4x4 previousViewToWorld);

        viewToWorld.Translation -= previousViewToWorld.Translation;
        Matrix4x4 previousView = args.PreviousView;
        previousView.Translation = Vector3.Zero;

        Matrix4x4 viewToPrevView = viewToWorld * previousView;
        Matrix4x4.Invert(viewToPrevView, out Matrix4x4 prevViewToView);
        Matrix4x4.Invert(args.Projection, out Matrix4x4 clipToView);
        Matrix4x4.Invert(args.PreviousProjection, out Matrix4x4 prevClipToView);

        Vector2 scale = new(1.0f / args.Input.Width, 1.0f / args.Input.Height);
        DLSSFrameGenerationArgs parameters = new()
        {
            Color = args.Color,
            Depth = args.Input.Depth,
            MotionVectors = args.Input.MotionVectors,
            OutputDisableInterpolation = disableInterpolation,
            GeneratedFrameCount = (uint)colors.Length,
            JitterOffset = -args.JitterOffset * scale * new Vector2(2.0f, -2.0f),
            MotionVectorScale = scale,
            ViewToClip = args.Projection,
            ClipToView = clipToView,
            ClipToPrevClip = clipToView * viewToPrevView * args.PreviousProjection,
            PrevClipToClip = prevClipToView * prevViewToView * args.Projection,
            CameraPosition = args.Camera.Position,
            CameraUp = args.Camera.Up,
            CameraRight = args.Camera.Right,
            CameraForward = args.Camera.Forward,
            CameraNear = args.Camera.NearPlane,
            CameraFar = args.Camera.FarPlane,
            CameraFovAngleVer = float.DegreesToRadians(args.Camera.Fov),
            CameraAspectRatio = args.Camera.AspectRatio,
            Reset = args.Reset
        };

        for (int i = 0; i < colors.Length; i++)
        {
            parameters.Output = colors[i];
            parameters.GeneratedFrameIndex = (uint)i;

            commandBuffer.Transition(colors[i], default, TextureLayout.Undefined, TextureLayout.Storage);
            dlss.Dispatch(commandBuffer, parameters);
            commandBuffer.Transition(colors[i], default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.CopyBuffer(disableInterpolation!, 0, Readback!, (uint)i * sizeof(uint), sizeof(uint));
        }
    }

    public void Resize(RenderDesc desc)
    {
        if (colors.Length == (int)desc.FrameGeneration && (dlss is null || (dlss.Desc.InputWidth == desc.InputWidth && dlss.Desc.InputHeight == desc.InputHeight && dlss.Desc.OutputWidth == desc.OutputWidth && dlss.Desc.OutputHeight == desc.OutputHeight)))
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

        dlss = App.Context.CreateDLSSFrameGeneration(new()
        {
            Format = PixelFormat.R8G8B8A8UNorm,
            InputWidth = desc.InputWidth,
            InputHeight = desc.InputHeight,
            OutputWidth = desc.OutputWidth,
            OutputHeight = desc.OutputHeight
        });

        colors = new Texture[(int)desc.FrameGeneration];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, desc.OutputWidth, desc.OutputHeight, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.Sampled | TextureUsages.Storage
            });
        }

        disableInterpolation = App.Context.CreateBuffer(new()
        {
            SizeInBytes = sizeof(uint),
            Usages = BufferUsages.StorageReadWrite | BufferUsages.TransferSrc,
            Residency = MemoryResidency.GpuOnly
        });
        Readback = App.Context.CreateBuffer(new()
        {
            SizeInBytes = sizeof(uint) * (uint)colors.Length,
            Usages = BufferUsages.TransferDst,
            Residency = MemoryResidency.CpuReadOnly
        });
    }

    private void DestroyResources()
    {
        Readback?.Dispose();
        Readback = null;
        disableInterpolation?.Dispose();
        disableInterpolation = null;

        for (int i = colors.Length - 1; i >= 0; i--)
        {
            colors[i]?.Dispose();
        }
        colors = [];

        dlss?.Dispose();
        dlss = null;
    }
}
