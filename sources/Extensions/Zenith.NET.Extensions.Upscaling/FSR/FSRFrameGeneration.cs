using System.Numerics;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRFrameGeneration(FSRVendor vendor, FrameGenerationDesc desc) : FrameGeneration(desc)
{
    private const float DefaultMinLuminanceInNits = 0.001f;
    private const float DefaultMaxLuminanceInNits = 1000.0f;

    private FSRContext context;
    private (FSRApiCreateContextFrameGenerationFlags Flags, PixelFormat ColorFormat, PixelFormat? HudlessFormat) key;
    private bool recreated;
    private ulong frameIndex;

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, _, bool depthReversed, bool depthInfinite) = Vendor.DecomposePerspective(args.Projection);
        bool isHdr = args.Color.Desc.Format.IsHdr();

        FSRApiCreateContextFrameGenerationFlags flags = default;

        if (isHdr)
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.HighDynamicRange;
        }

        if (depthReversed)
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInverted;
        }

        if (depthInfinite)
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInfinite;
        }

        EnsureContext((flags, args.Color.Desc.Format, args.Hudless?.Desc.Format));

        if (recreated)
        {
            args.Reset = true;
            recreated = false;
        }

        commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Output, default, TextureLayout.Storage, TextureLayout.Common);

        if (args.Hudless is not null)
        {
            commandBuffer.Transition(args.Hudless, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        commandBuffer.BeginExternalCommands();

        void* commandList = commandBuffer.FSR();
        FSRApiRect2D generationRect = new()
        {
            Width = (int)Desc.OutputWidth,
            Height = (int)Desc.OutputHeight
        };
        FSRConfigureDescFrameGeneration configureDesc = new()
        {
            FrameGenerationEnabled = true,
            HUDLessColor = args.Hudless.FSR(),
            Flags = (uint)FSRApiDispatchFrameGenerationFlags.NoSwapChainContextNotify,
            GenerationRect = generationRect,
            FrameID = frameIndex
        };

        vendor.Configure(context, &configureDesc.Header);

        float far = depthInfinite ? float.MaxValue : farPlane;

        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);

        Vector3 position = viewToWorld.Translation;
        Vector3 up = new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23);
        Vector3 right = new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13);
        Vector3 forward = new(viewToWorld.M31, viewToWorld.M32, viewToWorld.M33);
        forward = args.Projection.M34 < 0.0f ? -forward : forward;

        FSRDispatchDescFrameGenerationPrepareV2 prepareDesc = new()
        {
            FrameID = frameIndex,
            CommandList = commandList,
            RenderSize = new()
            {
                Width = Desc.RenderWidth,
                Height = Desc.RenderHeight
            },
            JitterOffset = new()
            {
                X = -args.JitterOffset.X,
                Y = -args.JitterOffset.Y
            },
            MotionVectorScale = new()
            {
                X = Desc.RenderWidth,
                Y = Desc.RenderHeight
            },
            FrameTimeDelta = args.DeltaTimeInSeconds * 1000.0f,
            Reset = args.Reset,
            CameraNear = depthReversed ? far : nearPlane,
            CameraFar = depthReversed ? nearPlane : far,
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

        vendor.Dispatch(context, &prepareDesc.Header);

        FSRApiBackBufferTransferFunction transferFunction = FSRApiBackBufferTransferFunction.Srgb;

        if (isHdr)
        {
            transferFunction = args.Color.Desc.Format is PixelFormat.R10G10B10A2UNorm ? FSRApiBackBufferTransferFunction.Pq : FSRApiBackBufferTransferFunction.Scrgb;
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
        dispatchDesc.MinMaxLuminance[0] = args.MinLuminanceInNits ?? DefaultMinLuminanceInNits;
        dispatchDesc.MinMaxLuminance[1] = args.MaxLuminanceInNits ?? DefaultMaxLuminanceInNits;

        vendor.Dispatch(context, &dispatchDesc.Header);

        commandBuffer.EndExternalCommands();

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
        vendor.DestroyContext(context);
        context = default;
    }

    private void EnsureContext((FSRApiCreateContextFrameGenerationFlags Flags, PixelFormat ColorFormat, PixelFormat? HudlessFormat) nextKey)
    {
        if (!context.IsNull && key == nextKey)
        {
            return;
        }

        if (!context.IsNull)
        {
            vendor.Context.GraphicsQueue.Timeline.Signal().Wait();
            vendor.Context.PresentQueue.Timeline.Signal().Wait();
            vendor.DestroyContext(context);
            context = default;
        }

        key = nextKey;

        FSRCreateContextDescFrameGenerationHudless hudless = new();
        FSRCreateContextDescFrameGenerationVersion version = new() { Version = (uint)Fsr.FrameGenerationVersion };
        FSRCreateContextDescFrameGeneration createDesc = new()
        {
            Flags = (uint)nextKey.Flags,
            DisplaySize = new()
            {
                Width = Desc.OutputWidth,
                Height = Desc.OutputHeight
            },
            MaxRenderSize = new()
            {
                Width = Desc.RenderWidth,
                Height = Desc.RenderHeight
            },
            BackBufferFormat = (uint)FSRFormats.FSR(nextKey.ColorFormat)
        };
        createDesc.Header.Next = &version.Header;

        if (nextKey.HudlessFormat is PixelFormat hudlessFormat)
        {
            hudless.HudlessBackBufferFormat = (uint)FSRFormats.FSR(hudlessFormat);
            version.Header.Next = &hudless.Header;
        }

        context = vendor.CreateContext(&createDesc.Header);
        recreated = true;
    }
}
