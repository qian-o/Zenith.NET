using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRSuperResolution : SuperResolution
{
    public FSRVendor Vendor;

    public FSRContext Context;

    public FSRSuperResolution(FSRVendor vendor, SuperResolutionDesc desc) : base(desc)
    {
        Vendor = vendor;

        FSRApiCreateContextUpscaleFlags flags = default;

        if (desc.IsHdr)
        {
            flags |= FSRApiCreateContextUpscaleFlags.HighDynamicRange;
        }

        if (desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed))
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInverted;
        }

        if (desc.Flags.HasFlag(UpscalingInputFlags.DepthInfinite))
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInfinite;
        }

        if (desc.Flags.HasFlag(UpscalingInputFlags.AutoExposure))
        {
            flags |= FSRApiCreateContextUpscaleFlags.AutoExposure;
        }

        FSRCreateContextDescUpscaleVersion version = new() { Version = (uint)Fsr.UpscalerVersion };
        FSRCreateContextDescUpscale createDesc = new()
        {
            Flags = (uint)flags,
            MaxRenderSize = new()
            {
                Width = desc.InputWidth,
                Height = desc.InputHeight
            },
            MaxUpscaleSize = new()
            {
                Width = desc.OutputWidth,
                Height = desc.OutputHeight
            }
        };
        createDesc.Header.Next = &version.Header;

        vendor.CreateContext(ref Context, &createDesc.Header);
    }

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args, Texture? transparencyMask)
    {
        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, _) = UpscalingHelper.DecomposePerspective(args.ViewToClip);
        float far = Desc.Flags.HasFlag(UpscalingInputFlags.DepthInfinite) ? float.MaxValue : farPlane;

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
                X = args.JitterOffset.X,
                Y = args.JitterOffset.Y
            },
            MotionVectorScale = new()
            {
                X = Desc.InputWidth,
                Y = Desc.InputHeight
            },
            RenderSize = new()
            {
                Width = Desc.InputWidth,
                Height = Desc.InputHeight
            },
            UpscaleSize = new()
            {
                Width = Desc.OutputWidth,
                Height = Desc.OutputHeight
            },
            FrameTimeDelta = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure,
            Reset = args.Reset,
            CameraNear = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed) ? far : nearPlane,
            CameraFar = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed) ? nearPlane : far,
            CameraFovAngleVertical = verticalFieldOfViewInRadians,
            ViewSpaceToMetersFactor = 1.0f
        };

        Vendor.Dispatch(ref Context, &dispatchDesc.Header);

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
        Vendor.DestroyContext(ref Context);
    }
}
