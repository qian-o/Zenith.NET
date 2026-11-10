using System.Numerics;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRFrameGeneration : FrameGeneration
{
    public FSRVendor Vendor;

    public FSRContext Context;

    private ulong frameIndex;

    public FSRFrameGeneration(FSRVendor vendor, FrameGenerationDesc desc) : base(desc)
    {
        Vendor = vendor;

        FSRApiCreateContextFrameGenerationFlags flags = default;

        if (desc.IsHdr)
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.HighDynamicRange;
        }

        if (desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed))
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInverted;
        }

        if (desc.Flags.HasFlag(UpscalingInputFlags.DepthInfinite))
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInfinite;
        }

        FSRCreateContextDescFrameGenerationHudless hudless = new();
        FSRCreateContextDescFrameGenerationVersion version = new() { Version = (uint)Fsr.FrameGenerationVersion };
        FSRCreateContextDescFrameGeneration createDesc = new()
        {
            Flags = (uint)flags,
            DisplaySize = new()
            {
                Width = desc.Width,
                Height = desc.Height
            },
            MaxRenderSize = new()
            {
                Width = desc.AuxiliaryWidth,
                Height = desc.AuxiliaryHeight
            },
            BackBufferFormat = (uint)FSRFormats.FSR(desc.ColorFormat)
        };
        createDesc.Header.Next = &version.Header;

        if (desc.HudlessFormat is PixelFormat hudlessFormat)
        {
            hudless.HudlessBackBufferFormat = (uint)FSRFormats.FSR(hudlessFormat);
            version.Header.Next = &hudless.Header;
        }

        vendor.CreateContext(ref Context, &createDesc.Header);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Output, default, TextureLayout.Storage, TextureLayout.Common);

        if (args.Hudless is not null)
        {
            commandBuffer.Transition(args.Hudless, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        void* commandList = commandBuffer.FSR();
        FSRApiRect2D generationRect = new()
        {
            Width = (int)Desc.Width,
            Height = (int)Desc.Height
        };
        FSRConfigureDescFrameGeneration configureDesc = new()
        {
            FrameGenerationEnabled = true,
            HUDLessColor = args.Hudless.FSR(),
            Flags = (uint)FSRApiDispatchFrameGenerationFlags.NoSwapChainContextNotify,
            GenerationRect = generationRect,
            FrameID = frameIndex
        };

        Vendor.Configure(ref Context, &configureDesc.Header);

        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, _) = UpscalingHelper.DecomposePerspective(args.ViewToClip);
        float far = Desc.Flags.HasFlag(UpscalingInputFlags.DepthInfinite) ? float.MaxValue : farPlane;

        Matrix4x4.Invert(args.WorldToView, out Matrix4x4 viewToWorld);

        Vector3 position = viewToWorld.Translation;
        Vector3 up = new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23);
        Vector3 right = new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13);
        Vector3 forward = new(viewToWorld.M31, viewToWorld.M32, viewToWorld.M33);
        forward = args.ViewToClip.M34 < 0.0f ? -forward : forward;

        FSRDispatchDescFrameGenerationPrepareV2 prepareDesc = new()
        {
            FrameID = frameIndex,
            CommandList = commandList,
            RenderSize = new()
            {
                Width = Desc.AuxiliaryWidth,
                Height = Desc.AuxiliaryHeight
            },
            JitterOffset = new()
            {
                X = args.JitterOffset.X,
                Y = args.JitterOffset.Y
            },
            MotionVectorScale = new()
            {
                X = Desc.AuxiliaryWidth,
                Y = Desc.AuxiliaryHeight
            },
            FrameTimeDelta = args.DeltaTimeInSeconds * 1000.0f,
            Reset = args.Reset,
            CameraNear = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed) ? far : nearPlane,
            CameraFar = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed) ? nearPlane : far,
            CameraFovAngleVertical = verticalFieldOfViewInRadians,
            ViewSpaceToMetersFactor = 1.0f,
            Depth = args.Depth.FSR(),
            MotionVectors = args.MotionVectors.FSR()
        };
        prepareDesc.CameraPosition[0] = position.X;
        prepareDesc.CameraPosition[1] = position.Y;
        prepareDesc.CameraPosition[2] = position.Z;
        prepareDesc.CameraUp[0] = up.X;
        prepareDesc.CameraUp[1] = up.Y;
        prepareDesc.CameraUp[2] = up.Z;
        prepareDesc.CameraRight[0] = right.X;
        prepareDesc.CameraRight[1] = right.Y;
        prepareDesc.CameraRight[2] = right.Z;
        prepareDesc.CameraForward[0] = forward.X;
        prepareDesc.CameraForward[1] = forward.Y;
        prepareDesc.CameraForward[2] = forward.Z;

        Vendor.Dispatch(ref Context, &prepareDesc.Header);

        FSRApiBackBufferTransferFunction transferFunction = FSRApiBackBufferTransferFunction.Srgb;

        if (Desc.IsHdr)
        {
            transferFunction = Desc.ColorFormat is PixelFormat.R16G16B16A16Float ? FSRApiBackBufferTransferFunction.Scrgb : FSRApiBackBufferTransferFunction.Pq;
        }

        FSRDispatchDescFrameGeneration dispatchDesc = new()
        {
            CommandList = commandList,
            PresentColor = args.Color.FSR(),
            NumGeneratedFrames = 1,
            Reset = args.Reset,
            BackBufferTransferFunction = (uint)transferFunction,
            GenerationRect = generationRect,
            FrameID = frameIndex
        };
        dispatchDesc.Outputs[0] = args.Output.FSR();
        dispatchDesc.MinMaxLuminance[0] = args.MinLuminanceInNits;
        dispatchDesc.MinMaxLuminance[1] = args.MaxLuminanceInNits;

        Vendor.Dispatch(ref Context, &dispatchDesc.Header);

        commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Output, default, TextureLayout.Common, TextureLayout.Storage);

        if (args.Hudless is not null)
        {
            commandBuffer.Transition(args.Hudless, default, TextureLayout.Common, TextureLayout.Sampled);
        }

        frameIndex++;
    }

    protected override void Destroy()
    {
        Vendor.DestroyContext(ref Context);
    }
}
