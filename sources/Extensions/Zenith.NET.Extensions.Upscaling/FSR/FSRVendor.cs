using System.Numerics;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRVendor : Vendor
{
    private readonly nint device;
    private readonly FSRVkDeviceContext deviceContext;

    public FSRVendor(GraphicsContext context) : base(context)
    {
        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                device = context.GetNativeObject(NativeObjectType.D3D12Device);
                break;

            case GraphicsApi.Vulkan:
                device = context.GetNativeObject(NativeObjectType.VulkanDevice);
                deviceContext = new()
                {
                    VkDevice = device,
                    VkPhysicalDevice = context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                    VkDeviceProcAddr = (delegate* unmanaged[Stdcall]<nint, byte*, delegate* unmanaged[Stdcall]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr)
                };
                break;
        }

        Capabilities = new(FSRHelper.IsSupported(context.GraphicsApi, device, deviceContext, Fsr.APICreateContextDescTypeUpscale), FSRHelper.IsSupported(context.GraphicsApi, device, deviceContext, Fsr.APICreateContextDescTypeDenoiser), FSRHelper.IsSupported(context.GraphicsApi, device, deviceContext, Fsr.APICreateContextDescTypeFrameGeneration));
    }

    public override UpscalingCapabilities Capabilities { get; }

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
        FSRCreateContextDescUpscale createDesc = new()
        {
            Flags = (uint)FSRHelper.Flags(args.Color, args.Exposure, args.Projection),
            MaxRenderSize = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight
            },
            MaxUpscaleSize = new()
            {
                Width = desc.OutputWidth,
                Height = desc.OutputHeight
            }
        };

        FSRContext context = default;

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                createDesc.AddNext(out FSRCreateBackendDX12Desc dx12Desc);
                dx12Desc.Device = device;

                Fsr.D3D12.CreateContext(&context, &createDesc.Header, null).Success();
                break;

            case GraphicsApi.Vulkan:
                createDesc.AddNext(out FSRCreateBackendVKDesc vkDesc);
                vkDesc.DeviceContext = deviceContext;

                Fsr.Vulkan.CreateContext(&context, &createDesc.Header, null).Success();
                break;
        }

        return context;
    }

    public FSRContext CreateFeature(RayReconstructionDesc desc)
    {
        FSRCreateContextDescDenoiser createDesc = new()
        {
            Version = (uint)Fsr.DenoiserVersion,
            MaxRenderSize = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight
            },
            SignalFlags = (uint)(FSRApiDenoiserSignalFlags.IndirectDiffuse | FSRApiDenoiserSignalFlags.DirectSpecular)
        };

        FSRContext context = default;

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                createDesc.AddNext(out FSRCreateBackendDX12Desc dx12Desc);
                dx12Desc.Device = device;

                Fsr.D3D12.CreateContext(&context, &createDesc.Header, null).Success();
                break;

            case GraphicsApi.Vulkan:
                createDesc.AddNext(out FSRCreateBackendVKDesc vkDesc);
                vkDesc.DeviceContext = deviceContext;

                Fsr.Vulkan.CreateContext(&context, &createDesc.Header, null).Success();
                break;
        }

        return context;
    }

    public FSRContext CreateFeature(FrameGenerationDesc desc, FrameGenerationArgs args)
    {
        FSRCreateContextDescFrameGeneration createDesc = new()
        {
            Flags = (uint)FSRHelper.Flags(args.Color, args.Projection),
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
            BackBufferFormat = (uint)FSRFormats.FSR(desc.Format)
        };

        if (args.Hudless is not null)
        {
            createDesc.AddNext(out FSRCreateContextDescFrameGenerationHudless hudlessDesc);
            hudlessDesc.HudlessBackBufferFormat = (uint)FSRFormats.FSR(args.Hudless.Desc.Format);
        }

        FSRContext context = default;

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                createDesc.AddNext(out FSRCreateBackendDX12Desc dx12Desc);
                dx12Desc.Device = device;

                Fsr.D3D12.CreateContext(&context, &createDesc.Header, null).Success();
                break;

            case GraphicsApi.Vulkan:
                createDesc.AddNext(out FSRCreateBackendVKDesc vkDesc);
                vkDesc.DeviceContext = deviceContext;

                Fsr.Vulkan.CreateContext(&context, &createDesc.Header, null).Success();
                break;
        }

        return context;
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

    public void EvaluateFeature(CommandBuffer commandBuffer, FSRContext context, SuperResolutionArgs args, Texture? transparencyMask, Texture output)
    {
        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out _, out bool depthReversed, out bool depthInfinite);

        float far = depthInfinite ? float.MaxValue : farPlane;

        FSRDispatchDescUpscale dispatchDesc = new()
        {
            CommandList = commandBuffer.FSR(),
            Color = args.Color.FSR(),
            Depth = args.Depth.FSR(),
            MotionVectors = args.MotionVectors.FSR(),
            Exposure = args.Exposure?.FSR() ?? default,
            Reactive = args.ReactiveMask?.FSR() ?? default,
            TransparencyAndComposition = transparencyMask?.FSR() ?? default,
            Output = output.FSR(),
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
            RenderSize = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            UpscaleSize = new()
            {
                Width = output.Desc.Width,
                Height = output.Desc.Height
            },
            FrameTimeDelta = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure ?? 1.0f,
            Reset = args.Reset,
            CameraNear = depthReversed ? far : nearPlane,
            CameraFar = depthReversed ? nearPlane : far,
            CameraFovAngleVertical = verticalFieldOfViewInRadians,
            ViewSpaceToMetersFactor = 1.0f
        };

        FSRHelper.Dispatch(Context.GraphicsApi, context, &dispatchDesc.Header);
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, FSRContext context, RayReconstructionArgs args, Texture linearDepth, Texture motionVectors, Texture normals, Texture diffuseInput, Texture diffuseOutput, Texture specularInput, Texture specularOutput, Vector3 cameraPositionDelta, uint frameIndex)
    {
        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out _, out _, out _, out bool depthInfinite);

        FSRDispatchDescDenoiser dispatchDesc = new()
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
            View = Fsr.ConvertToMatrix4x4((float*)&args.View, FSRApiMatrixLayout.RowMajor, FSRApiMatrixVectorConvention.RowVectors),
            Projection = Fsr.ConvertToMatrix4x4((float*)&args.Projection, FSRApiMatrixLayout.RowMajor, FSRApiMatrixVectorConvention.RowVectors),
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

        dispatchDesc.AddNext(out FSRDispatchDescDenoiserIndirectDiffuse diffuseSignal);
        diffuseSignal.Signal = new()
        {
            Input = diffuseInput.FSR(),
            Output = diffuseOutput.FSR()
        };

        dispatchDesc.AddNext(out FSRDispatchDescDenoiserDirectSpecular specularSignal);
        specularSignal.Signal = new()
        {
            Input = specularInput.FSR(),
            Output = specularOutput.FSR()
        };

        FSRHelper.Dispatch(Context.GraphicsApi, context, &dispatchDesc.Header);
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, FSRContext context, FrameGenerationArgs args, Texture output, ulong frameId)
    {
        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out _, out bool depthReversed, out bool depthInfinite);

        float far = depthInfinite ? float.MaxValue : farPlane;

        FSRApiRect2D generationRect = new()
        {
            Width = (int)output.Desc.Width,
            Height = (int)output.Desc.Height
        };

        FSRConfigureDescFrameGeneration configureDesc = new()
        {
            FrameGenerationEnabled = true,
            HUDLessColor = args.Hudless?.FSR() ?? default,
            Flags = (uint)FSRApiDispatchFrameGenerationFlags.NoSwapChainContextNotify,
            GenerationRect = generationRect,
            FrameID = frameId
        };

        FSRHelper.Configure(Context.GraphicsApi, context, &configureDesc.Header);

        UpscalingHelper.DecomposeCamera(args.View, args.Projection, out Vector3 position, out Vector3 up, out Vector3 right, out Vector3 forward);

        FSRDispatchDescFrameGenerationPrepareV2 prepareDesc = new()
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
            CameraNear = depthReversed ? far : nearPlane,
            CameraFar = depthReversed ? nearPlane : far,
            CameraFovAngleVertical = verticalFieldOfViewInRadians,
            ViewSpaceToMetersFactor = 1.0f,
            Depth = args.Depth.FSR(),
            MotionVectors = args.MotionVectors.FSR()
        };
        *(Vector3*)&prepareDesc.CameraPosition = position;
        *(Vector3*)&prepareDesc.CameraUp = up;
        *(Vector3*)&prepareDesc.CameraRight = right;
        *(Vector3*)&prepareDesc.CameraForward = forward;

        FSRHelper.Dispatch(Context.GraphicsApi, context, &prepareDesc.Header);

        FSRDispatchDescFrameGeneration dispatchDesc = new()
        {
            CommandList = commandBuffer.FSR(),
            PresentColor = args.Color.FSR(),
            NumGeneratedFrames = 1,
            Reset = args.Reset,
            BackBufferTransferFunction = (uint)(args.Color.Desc.Format switch
            {
                PixelFormat.R16G16B16A16Float => FSRApiBackBufferTransferFunction.Scrgb,
                PixelFormat.R10G10B10A2UNorm => FSRApiBackBufferTransferFunction.Pq,
                _ => FSRApiBackBufferTransferFunction.Srgb
            }),
            GenerationRect = generationRect,
            FrameID = frameId
        };
        dispatchDesc.Outputs[0] = output.FSR();

        FSRHelper.Dispatch(Context.GraphicsApi, context, &dispatchDesc.Header);
    }

    protected override void Destroy()
    {
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

file static unsafe class FSRHelper
{
    public static bool IsSupported(GraphicsApi graphicsApi, nint device, FSRVkDeviceContext deviceContext, ulong createDescType)
    {
        ulong count = 0;

        FSRQueryDescGetVersions queryDesc = new()
        {
            CreateDescType = createDescType,
            OutputCount = &count
        };

        switch (graphicsApi)
        {
            case GraphicsApi.DirectX12:
                queryDesc.Device = (void*)device;

                return Fsr.D3D12.Query(null, &queryDesc.Header) is FSRApiReturnCodes.Ok && count > 0;

            case GraphicsApi.Vulkan:
                queryDesc.AddNext(out FSRCreateBackendVKDesc backendDesc);
                backendDesc.DeviceContext = deviceContext;

                return Fsr.Vulkan.Query(null, &queryDesc.Header) is FSRApiReturnCodes.Ok && count > 0;

            default:
                return false;
        }
    }

    public static void Configure(GraphicsApi graphicsApi, FSRContext context, FSRApiHeader* desc)
    {
        switch (graphicsApi)
        {
            case GraphicsApi.DirectX12:
                Fsr.D3D12.Configure(&context, desc).Success();
                break;

            case GraphicsApi.Vulkan:
                Fsr.Vulkan.Configure(&context, desc).Success();
                break;
        }
    }

    public static void Dispatch(GraphicsApi graphicsApi, FSRContext context, FSRApiHeader* desc)
    {
        switch (graphicsApi)
        {
            case GraphicsApi.DirectX12:
                Fsr.D3D12.Dispatch(&context, desc).Success();
                break;

            case GraphicsApi.Vulkan:
                Fsr.Vulkan.Dispatch(&context, desc).Success();
                break;
        }
    }

    public static FSRApiCreateContextUpscaleFlags Flags(Texture color, Texture? exposure, Matrix4x4 projection)
    {
        UpscalingHelper.DecomposePerspective(projection, out _, out _, out _, out _, out bool depthReversed, out bool depthInfinite);

        FSRApiCreateContextUpscaleFlags flags = default;

        if (UpscalingHelper.IsFloat(color.Desc.Format))
        {
            flags |= FSRApiCreateContextUpscaleFlags.HighDynamicRange;
        }

        if (exposure is null)
        {
            flags |= FSRApiCreateContextUpscaleFlags.AutoExposure;
        }

        if (depthReversed)
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInverted;
        }

        if (depthInfinite)
        {
            flags |= FSRApiCreateContextUpscaleFlags.DepthInfinite;
        }

        return flags;
    }

    public static FSRApiCreateContextFrameGenerationFlags Flags(Texture color, Matrix4x4 projection)
    {
        UpscalingHelper.DecomposePerspective(projection, out _, out _, out _, out _, out bool depthReversed, out bool depthInfinite);

        FSRApiCreateContextFrameGenerationFlags flags = default;

        if (depthReversed)
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInverted;
        }

        if (depthInfinite)
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.DepthInfinite;
        }

        if (UpscalingHelper.IsHdr(color.Desc.Format))
        {
            flags |= FSRApiCreateContextFrameGenerationFlags.HighDynamicRange;
        }

        return flags;
    }
}
