using System.Numerics;
using System.Runtime.CompilerServices;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRVendor : Vendor
{
    private readonly nint device;
    private readonly FSRVkDeviceContext deviceContext;

    private ComputePipeline? preparePipeline;
    private ComputePipeline? composePipeline;

    public FSRVendor(GraphicsContext context) : base(context)
    {
        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                device = context.DirectX12();
                break;

            case GraphicsApi.Vulkan:
                deviceContext = context.Vulkan();
                break;
        }

        SuperResolutionSupported = IsSupported(Fsr.APICreateContextDescTypeUpscale);
        RayReconstructionSupported = IsSupported(Fsr.APICreateContextDescTypeDenoiser);
        FrameGenerationSupported = IsSupported(Fsr.APICreateContextDescTypeFrameGeneration);
    }

    public override bool SuperResolutionSupported { get; }

    public override bool RayReconstructionSupported { get; }

    public override bool FrameGenerationSupported { get; }

    public ComputePipeline PreparePipeline => preparePipeline ??= UpscalingBridge.CreatePipeline(Context, UpscalingBridge.RayReconstructionPrepareCS(Context.GraphicsApi));

    public ComputePipeline ComposePipeline => composePipeline ??= UpscalingBridge.CreatePipeline(Context, UpscalingBridge.RayReconstructionComposeCS(Context.GraphicsApi));

    public override SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return new FSRSuperResolution(this, desc);
    }

    public override RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return new FSRRayReconstruction(this, desc);
    }

    public override FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return new FSRFrameGeneration(this, desc);
    }

    public FSRContext CreateFeature(SuperResolutionDesc desc, SuperResolutionArgs args)
    {
        FSRCreateContextDescUpscale createDesc = ParameterBuilder.CreationParams(desc, args);

        return CreateContext(ref createDesc);
    }

    public void CreateFeature(RayReconstructionDesc desc, RayReconstructionArgs args, out FSRContext denoiser, out FSRContext upscaler)
    {
        (FSRCreateContextDescDenoiser denoiserDesc, FSRCreateContextDescUpscale upscalerDesc) = ParameterBuilder.CreationParams(desc, args);

        denoiser = CreateContext(ref denoiserDesc);
        upscaler = CreateContext(ref upscalerDesc);
    }

    public FSRContext CreateFeature(FrameGenerationDesc desc, FrameGenerationArgs args)
    {
        FSRCreateContextDescFrameGeneration createDesc = ParameterBuilder.CreationParams(desc, args);

        if (args.Hudless is not null)
        {
            createDesc.AddNext(out FSRCreateContextDescFrameGenerationHudless hudlessDesc);
            hudlessDesc.HudlessBackBufferFormat = (uint)FSRFormats.FSR(args.Hudless.Desc.Format).Format;
        }

        return CreateContext(ref createDesc);
    }

    public void DestroyFeature(FSRContext context)
    {
        if (context.IsNull)
        {
            return;
        }

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Fsr.D3D12.DestroyContext(&context, null).Success();
                break;

            case GraphicsApi.Vulkan:
                Fsr.Vulkan.DestroyContext(&context, null).Success();
                break;
        }
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, FSRContext context, SuperResolutionArgs args, Texture output)
    {
        FSRDispatchDescUpscale dispatchDesc = ParameterBuilder.EvaluationParams(commandBuffer, args, output);

        Dispatch(context, ref dispatchDesc);
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, FSRContext context, RayReconstructionArgs args, Texture color, Texture? transparencyMask, Texture output)
    {
        FSRDispatchDescUpscale dispatchDesc = ParameterBuilder.EvaluationParams(commandBuffer, args, color, transparencyMask, output);

        Dispatch(context, ref dispatchDesc);
    }

    public void EvaluateFeature(CommandBuffer commandBuffer,
                                FSRContext context,
                                RayReconstructionArgs args,
                                Texture linearDepth,
                                Texture motionVectors,
                                Texture normals,
                                Texture diffuseInput,
                                Texture diffuseOutput,
                                Texture specularInput,
                                Texture specularOutput,
                                Vector3 cameraPositionDelta,
                                uint frameIndex)
    {
        FSRDispatchDescDenoiser dispatchDesc = ParameterBuilder.EvaluationParams(commandBuffer, args, linearDepth, motionVectors, normals, cameraPositionDelta, frameIndex);

        dispatchDesc.AddNext(out FSRDispatchDescDenoiserIndirectDiffuse diffuseSignal);
        diffuseSignal.Signal = new()
        {
            Input = diffuseInput.FSR(),
            Output = diffuseOutput.FSR()
        };

        dispatchDesc.AddNext(out FSRDispatchDescDenoiserIndirectSpecular specularSignal);
        specularSignal.Signal = new()
        {
            Input = specularInput.FSR(),
            Output = specularOutput.FSR()
        };

        Dispatch(context, ref dispatchDesc);
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, FSRContext context, FrameGenerationArgs args, Texture output, ulong frameId)
    {
        (FSRConfigureDescFrameGeneration configureDesc, FSRDispatchDescFrameGenerationPrepareV2 prepareDesc, FSRDispatchDescFrameGeneration dispatchDesc) = ParameterBuilder.EvaluationParams(commandBuffer, args, output, frameId);

        Configure(context, ref configureDesc);
        Dispatch(context, ref prepareDesc);
        Dispatch(context, ref dispatchDesc);
    }

    protected override void Destroy()
    {
        composePipeline?.Dispose();
        preparePipeline?.Dispose();
    }

    private bool IsSupported(ulong createDescType)
    {
        ulong count = 0;

        FSRQueryDescGetVersions queryDesc = new()
        {
            CreateDescType = createDescType,
            OutputCount = &count
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                queryDesc.Device = (void*)device;

                return Fsr.D3D12.Query(null, &queryDesc.Header) is FSRApiReturnCodes.Ok && count > 0;

            case GraphicsApi.Vulkan:
                queryDesc.AddNext(out FSRCreateBackendVKDesc vkDesc);
                vkDesc.DeviceContext = deviceContext;

                return Fsr.Vulkan.Query(null, &queryDesc.Header) is FSRApiReturnCodes.Ok && count > 0;

            default:
                return false;
        }
    }

    private FSRContext CreateContext<T>(ref T desc) where T : unmanaged
    {
        FSRContext context = default;

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                desc.AddNext(out FSRCreateBackendDX12Desc dx12Desc);
                dx12Desc.Device = device;

                Fsr.D3D12.CreateContext(&context, (FSRApiHeader*)Unsafe.AsPointer(ref desc), null).Success();
                break;

            case GraphicsApi.Vulkan:
                desc.AddNext(out FSRCreateBackendVKDesc vkDesc);
                vkDesc.DeviceContext = deviceContext;

                Fsr.Vulkan.CreateContext(&context, (FSRApiHeader*)Unsafe.AsPointer(ref desc), null).Success();
                break;
        }

        return context;
    }

    private void Configure<T>(FSRContext context, ref T desc) where T : unmanaged
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Fsr.D3D12.Configure(&context, (FSRApiHeader*)Unsafe.AsPointer(ref desc)).Success();
                break;

            case GraphicsApi.Vulkan:
                Fsr.Vulkan.Configure(&context, (FSRApiHeader*)Unsafe.AsPointer(ref desc)).Success();
                break;
        }
    }

    private void Dispatch<T>(FSRContext context, ref T desc) where T : unmanaged
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Fsr.D3D12.Dispatch(&context, (FSRApiHeader*)Unsafe.AsPointer(ref desc)).Success();
                break;

            case GraphicsApi.Vulkan:
                Fsr.Vulkan.Dispatch(&context, (FSRApiHeader*)Unsafe.AsPointer(ref desc)).Success();
                break;
        }
    }

    public static void Transition(CommandBuffer commandBuffer, ReadOnlySpan<Texture?> textures, TextureLayout before, TextureLayout after)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] is Texture texture && !textures[..i].Contains(texture))
            {
                commandBuffer.Transition(texture, default, before, after);
            }
        }
    }
}

file static unsafe class ParameterBuilder
{
    public static FSRCreateContextDescUpscale CreationParams(SuperResolutionDesc desc, SuperResolutionArgs args)
    {
        return CreationParams(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight, args.Color.Desc.Format, args.Exposure, args.Projection);
    }

    public static (FSRCreateContextDescDenoiser Denoiser, FSRCreateContextDescUpscale Upscaler) CreationParams(RayReconstructionDesc desc, RayReconstructionArgs args)
    {
        FSRCreateContextDescDenoiser denoiser = new()
        {
            Version = (uint)Fsr.DenoiserVersion,
            MaxRenderSize = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight
            },
            SignalFlags = (uint)(FSRApiDenoiserSignalFlags.IndirectDiffuse | FSRApiDenoiserSignalFlags.IndirectSpecular)
        };

        FSRCreateContextDescUpscale upscaler = CreationParams(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight, PixelFormat.R16G16B16A16Float, args.Exposure, args.Projection);

        return (denoiser, upscaler);
    }

    public static FSRCreateContextDescFrameGeneration CreationParams(FrameGenerationDesc desc, FrameGenerationArgs args)
    {
        return new()
        {
            Flags = (uint)Flags(args.Color.Desc.Format, args.Projection),
            DisplaySize = new()
            {
                Width = desc.OutputWidth,
                Height = desc.OutputHeight
            },
            MaxRenderSize = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight
            },
            BackBufferFormat = (uint)FSRFormats.FSR(desc.Format).Format
        };
    }

    public static FSRDispatchDescUpscale EvaluationParams(CommandBuffer commandBuffer, SuperResolutionArgs args, Texture output)
    {
        return EvaluationParams(commandBuffer,
                                args.Color,
                                args.Depth,
                                args.MotionVectors,
                                args.Exposure,
                                args.ReactiveMask,
                                null,
                                output,
                                args.JitterOffset,
                                args.Projection,
                                args.DeltaTimeInSeconds,
                                args.PreExposure,
                                args.Reset);
    }

    public static FSRDispatchDescUpscale EvaluationParams(CommandBuffer commandBuffer, RayReconstructionArgs args, Texture color, Texture? transparencyMask, Texture output)
    {
        return EvaluationParams(commandBuffer,
                                color,
                                args.Depth,
                                args.MotionVectors,
                                args.Exposure,
                                args.ReactiveMask,
                                transparencyMask,
                                output,
                                args.JitterOffset,
                                args.Projection,
                                args.DeltaTimeInSeconds,
                                args.PreExposure,
                                args.Reset);
    }

    public static FSRDispatchDescDenoiser EvaluationParams(CommandBuffer commandBuffer, RayReconstructionArgs args, Texture linearDepth, Texture motionVectors, Texture normals, Vector3 cameraPositionDelta, uint frameIndex)
    {
        (float nearPlane, float farPlane, _, _, _, bool depthInfinite) = UpscalingHelper.DecomposePerspective(args.Projection);

        return new()
        {
            CommandList = commandBuffer.FSR(),
            LinearDepth = linearDepth.FSR(),
            MotionVectors = motionVectors.FSR(),
            Normals = normals.FSR(),
            SpecularAlbedo = args.SpecularAlbedo.FSR(),
            DiffuseAlbedo = args.DiffuseAlbedo.FSR(),
            MotionVectorScale = new()
            {
                X = 1.0f,
                Y = 1.0f,
                Z = 1.0f
            },
            JitterOffsets = new()
            {
                X = -args.JitterOffset.X,
                Y = -args.JitterOffset.Y
            },
            CameraPositionDelta = new()
            {
                X = cameraPositionDelta.X,
                Y = cameraPositionDelta.Y,
                Z = cameraPositionDelta.Z
            },
            View = FSRFormats.FSR(args.View),
            Projection = FSRFormats.FSR(args.Projection),
            LinearDepthBounds = new()
            {
                Min = nearPlane,
                Max = depthInfinite ? 1e6f : farPlane
            },
            RenderSize = new()
            {
                Width = linearDepth.Desc.Width,
                Height = linearDepth.Desc.Height
            },
            FrameIndex = frameIndex,
            Flags = (uint)((args.Reset ? FSRApiDispatchDenoiserFlags.Reset : 0) | FSRApiDispatchDenoiserFlags.NonEncodedAlbedo)
        };
    }

    public static (FSRConfigureDescFrameGeneration Configure, FSRDispatchDescFrameGenerationPrepareV2 Prepare, FSRDispatchDescFrameGeneration Dispatch) EvaluationParams(CommandBuffer commandBuffer, FrameGenerationArgs args, Texture output, ulong frameId)
    {
        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, _, bool depthReversed, _) = UpscalingHelper.DecomposePerspective(args.Projection);
        (Vector3 position, Vector3 up, Vector3 right, Vector3 forward) = UpscalingHelper.DecomposeCamera(args.View, args.Projection);

        FSRApiRect2D generationRect = new()
        {
            Width = (int)output.Desc.Width,
            Height = (int)output.Desc.Height
        };

        FSRConfigureDescFrameGeneration configure = new()
        {
            FrameGenerationEnabled = true,
            HUDLessColor = args.Hudless is not null && args.Hudless != args.Color ? args.Hudless.FSR() : default,
            Flags = (uint)FSRApiDispatchFrameGenerationFlags.NoSwapChainContextNotify,
            GenerationRect = generationRect,
            FrameID = frameId
        };

        FSRDispatchDescFrameGenerationPrepareV2 prepare = new()
        {
            FrameID = frameId,
            Flags = (uint)FSRApiDispatchFrameGenerationFlags.NoSwapChainContextNotify,
            CommandList = commandBuffer.FSR(),
            RenderSize = new()
            {
                Width = args.Depth.Desc.Width,
                Height = args.Depth.Desc.Height
            },
            JitterOffset = new()
            {
                X = -args.JitterOffset.X,
                Y = -args.JitterOffset.Y
            },
            MotionVectorScale = new()
            {
                X = args.MotionVectors.Desc.Width,
                Y = args.MotionVectors.Desc.Height
            },
            FrameTimeDelta = args.DeltaTimeInSeconds * 1000.0f,
            Reset = args.Reset,
            CameraNear = depthReversed ? farPlane : nearPlane,
            CameraFar = depthReversed ? nearPlane : farPlane,
            CameraFovAngleVertical = verticalFieldOfViewInRadians,
            ViewSpaceToMetersFactor = 1.0f,
            Depth = args.Depth.FSR(),
            MotionVectors = args.MotionVectors.FSR()
        };
        *(Vector3*)&prepare.CameraPosition = position;
        *(Vector3*)&prepare.CameraUp = up;
        *(Vector3*)&prepare.CameraRight = right;
        *(Vector3*)&prepare.CameraForward = forward;

        FSRDispatchDescFrameGeneration dispatch = new()
        {
            CommandList = commandBuffer.FSR(),
            PresentColor = args.Color.FSR(),
            NumGeneratedFrames = 1,
            Reset = args.Reset,
            BackBufferTransferFunction = (uint)FSRFormats.FSR(args.Color.Desc.Format).TransferFunction,
            GenerationRect = generationRect,
            FrameID = frameId
        };
        dispatch.Outputs[0] = output.FSR();

        return (configure, prepare, dispatch);
    }

    private static FSRCreateContextDescUpscale CreationParams(uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight, PixelFormat format, Texture? exposure, Matrix4x4 projection)
    {
        return new()
        {
            Flags = (uint)Flags(format, exposure, projection),
            MaxRenderSize = new()
            {
                Width = renderWidth,
                Height = renderHeight
            },
            MaxUpscaleSize = new()
            {
                Width = outputWidth,
                Height = outputHeight
            }
        };
    }

    private static FSRDispatchDescUpscale EvaluationParams(CommandBuffer commandBuffer,
                                                           Texture color,
                                                           Texture depth,
                                                           Texture motionVectors,
                                                           Texture? exposure,
                                                           Texture? reactiveMask,
                                                           Texture? transparencyMask,
                                                           Texture output,
                                                           Vector2 jitterOffset,
                                                           Matrix4x4 projection,
                                                           float deltaTimeInSeconds,
                                                           float? preExposure,
                                                           bool reset)
    {
        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, _, bool depthReversed, _) = UpscalingHelper.DecomposePerspective(projection);

        return new()
        {
            CommandList = commandBuffer.FSR(),
            Color = color.FSR(),
            Depth = depth.FSR(),
            MotionVectors = motionVectors.FSR(),
            Exposure = exposure?.FSR() ?? default,
            Reactive = reactiveMask?.FSR() ?? default,
            TransparencyAndComposition = transparencyMask?.FSR() ?? default,
            Output = output.FSR(),
            JitterOffset = new()
            {
                X = -jitterOffset.X,
                Y = -jitterOffset.Y
            },
            MotionVectorScale = new()
            {
                X = motionVectors.Desc.Width,
                Y = motionVectors.Desc.Height
            },
            RenderSize = new()
            {
                Width = color.Desc.Width,
                Height = color.Desc.Height
            },
            UpscaleSize = new()
            {
                Width = output.Desc.Width,
                Height = output.Desc.Height
            },
            FrameTimeDelta = deltaTimeInSeconds * 1000.0f,
            PreExposure = preExposure ?? 1.0f,
            Reset = reset,
            CameraNear = depthReversed ? farPlane : nearPlane,
            CameraFar = depthReversed ? nearPlane : farPlane,
            CameraFovAngleVertical = verticalFieldOfViewInRadians,
            ViewSpaceToMetersFactor = 1.0f
        };
    }

    private static FSRApiCreateContextUpscaleFlags Flags(PixelFormat format, Texture? exposure, Matrix4x4 projection)
    {
        FSRApiCreateContextUpscaleFlags flags = default;

        if (UpscalingHelper.IsFloat(format))
        {
            flags |= FSRApiCreateContextUpscaleFlags.HighDynamicRange;
        }

        if (exposure is null)
        {
            flags |= FSRApiCreateContextUpscaleFlags.AutoExposure;
        }

        if (UpscalingHelper.IsDepthReversed(projection))
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInverted;
        }

        if (UpscalingHelper.IsDepthInfinite(projection))
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInfinite;
        }

        return flags;
    }

    private static FSRApiCreateContextFrameGenerationFlags Flags(PixelFormat format, Matrix4x4 projection)
    {
        FSRApiCreateContextFrameGenerationFlags flags = default;

        if (UpscalingHelper.IsHdr(format))
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.HighDynamicRange;
        }

        if (UpscalingHelper.IsDepthReversed(projection))
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInverted;
        }

        if (UpscalingHelper.IsDepthInfinite(projection))
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInfinite;
        }

        return flags;
    }
}
