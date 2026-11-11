using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRSuperResolution(FSRVendor vendor, SuperResolutionDesc desc) : SuperResolution(desc)
{
    private FSRContext context;
    private FSRApiCreateContextUpscaleFlags contextFlags;
    private bool recreated;

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args, Texture? transparencyMask)
    {
        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, _, bool depthReversed, bool depthInfinite) = Vendor.DecomposePerspective(args.Projection);
        FSRApiCreateContextUpscaleFlags flags = default;

        if (args.Color.Desc.Format.IsHdr())
        {
            flags |= FSRApiCreateContextUpscaleFlags.HighDynamicRange;
        }

        if (depthReversed)
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInverted;
        }

        if (depthInfinite)
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInfinite;
        }

        if (args.Exposure is null)
        {
            flags |= FSRApiCreateContextUpscaleFlags.AutoExposure;
        }

        EnsureContext(flags);

        if (recreated)
        {
            args.Reset = true;
            recreated = false;
        }

        float far = depthInfinite ? float.MaxValue : farPlane;

        commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Output, default, TextureLayout.Storage, TextureLayout.Common);

        if (args.Exposure is not null)
        {
            commandBuffer.Transition(args.Exposure, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        if (args.ReactiveMask is not null)
        {
            commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        if (transparencyMask is not null)
        {
            commandBuffer.Transition(transparencyMask, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        FSRDispatchDescUpscale dispatchDesc = new()
        {
            CommandList = commandBuffer.FSR(),
            Color = args.Color.FSR(),
            Depth = args.Depth.FSR(),
            MotionVectors = args.MotionVectors.FSR(),
            Exposure = args.Exposure.FSR(),
            Reactive = args.ReactiveMask.FSR(),
            TransparencyAndComposition = transparencyMask.FSR(),
            Output = args.Output.FSR(),
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
            RenderSize = new()
            {
                Width = Desc.RenderWidth,
                Height = Desc.RenderHeight
            },
            UpscaleSize = new()
            {
                Width = Desc.OutputWidth,
                Height = Desc.OutputHeight
            },
            FrameTimeDelta = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure ?? 1.0f,
            Reset = args.Reset,
            CameraNear = depthReversed ? far : nearPlane,
            CameraFar = depthReversed ? nearPlane : far,
            CameraFovAngleVertical = verticalFieldOfViewInRadians,
            ViewSpaceToMetersFactor = 1.0f
        };

        commandBuffer.BeginExternalCommands();

        vendor.Dispatch(context, &dispatchDesc.Header);

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Output, default, TextureLayout.Common, TextureLayout.Storage);

        if (args.Exposure is not null)
        {
            commandBuffer.Transition(args.Exposure, default, TextureLayout.Common, TextureLayout.Sampled);
        }

        if (args.ReactiveMask is not null)
        {
            commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Common, TextureLayout.Sampled);
        }

        if (transparencyMask is not null)
        {
            commandBuffer.Transition(transparencyMask, default, TextureLayout.Common, TextureLayout.Sampled);
        }
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        Dispatch(commandBuffer, args, null);
    }

    protected override void Destroy()
    {
        vendor.DestroyContext(context);
        context = default;
    }

    private void EnsureContext(FSRApiCreateContextUpscaleFlags flags)
    {
        if (!context.IsNull && contextFlags == flags)
        {
            return;
        }

        if (!context.IsNull)
        {
            vendor.Context.GraphicsQueue.Timeline.Signal().Wait();
            vendor.DestroyContext(context);
            context = default;
        }

        contextFlags = flags;

        FSRCreateContextDescUpscaleVersion version = new() { Version = (uint)Fsr.UpscalerVersion };
        FSRCreateContextDescUpscale createDesc = new()
        {
            Flags = (uint)flags,
            MaxRenderSize = new()
            {
                Width = Desc.RenderWidth,
                Height = Desc.RenderHeight
            },
            MaxUpscaleSize = new()
            {
                Width = Desc.OutputWidth,
                Height = Desc.OutputHeight
            }
        };
        createDesc.Header.Next = &version.Header;

        context = vendor.CreateContext(&createDesc.Header);
        recreated = true;
    }
}
