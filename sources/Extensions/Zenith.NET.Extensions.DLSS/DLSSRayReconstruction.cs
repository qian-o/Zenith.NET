using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSRayReconstruction : DisposableObject
{
    private readonly DLSSBackend backend;
    private readonly NGXParameter parameters;

    private NGXHandle handle;

    internal DLSSRayReconstruction(DLSSBackend backend, DLSSRayReconstructionDesc desc)
    {
        this.backend = backend;

        parameters = backend.AllocateParameters();

        Desc = desc;
    }

    public DLSSRayReconstructionDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSRayReconstructionArgs args)
    {
        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Ray Reconstruction");
        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);

        nint commandList = backend.CommandList(commandBuffer);

        backend.BindEmptyDescriptorSet(commandList);

        if (handle.IsNull)
        {
            Create(commandList);
        }

        Evaluate(commandList, args);

        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);
        commandBuffer.EndDebugEvent();
    }

    protected override void Destroy()
    {
        if (!handle.IsNull)
        {
            backend.ReleaseFeature(handle);
        }

        backend.DestroyParameters(parameters);
    }

    private void Create(nint commandList)
    {
        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = DLSSFormats.RoughnessMode(Desc),
            UseHWDepth = DLSSFormats.DepthType(Desc),
            Width = Desc.InputWidth,
            Height = Desc.InputHeight,
            TargetWidth = Desc.OutputWidth,
            TargetHeight = Desc.OutputHeight,
            PerfQualityValue = DLSSFormats.PerfQualityValue(Desc.Mode),
            FeatureCreateFlags = (int)DLSSFormats.FeatureFlags(Desc)
        };

        fixed (NGXHandle* pHandle = &handle)
        {
            switch (backend.Context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    Ngx.D3D12.CreateDLSSDExt(commandList, 1, 1, pHandle, parameters, &createParams).Success();
                    break;

                case GraphicsApi.Vulkan:
                    Ngx.Vulkan.CreateDLSSDExt1(backend.Device, commandList, 1, 1, pHandle, parameters, &createParams).Success();
                    break;
            }
        }
    }

    private void Evaluate(nint commandList, DLSSRayReconstructionArgs args)
    {
        NGXDimensions renderSubrectDimensions = new()
        {
            Width = Desc.InputWidth,
            Height = Desc.InputHeight
        };

        switch (backend.Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                {
                    NGXD3D12DLSSDEvalParams evalParams = new()
                    {
                        DiffuseAlbedo = args.DiffuseAlbedo.DirectX12,
                        SpecularAlbedo = args.SpecularAlbedo.DirectX12,
                        Normals = args.Normals.DirectX12,
                        Roughness = args.Roughness?.DirectX12 ?? 0,
                        Color = args.Input.DirectX12,
                        Output = args.Output.DirectX12,
                        Depth = args.Depth.DirectX12,
                        MotionVectors = args.MotionVectors.DirectX12,
                        JitterOffsetX = args.JitterOffsetX,
                        JitterOffsetY = args.JitterOffsetY,
                        RenderSubrectDimensions = renderSubrectDimensions,
                        Reset = args.Reset ? 1 : 0,
                        MVScaleX = args.MotionVectorScaleX,
                        MVScaleY = args.MotionVectorScaleY,
                        ExposureTexture = args.Exposure?.DirectX12 ?? 0,
                        BiasCurrentColorMask = args.ReactiveMask?.DirectX12 ?? 0,
                        ColorBeforeTransparency = args.ColorBeforeTransparency?.DirectX12 ?? 0,
                        ScreenSpaceSubsurfaceScatteringGuide = args.SubsurfaceScatteringGuide?.DirectX12 ?? 0,
                        DepthOfFieldGuide = args.DepthOfFieldGuide?.DirectX12 ?? 0,
                        SpecularHitDistance = args.SpecularHitDistance?.DirectX12 ?? 0,
                        WorldToViewMatrix = &args.WorldToView,
                        ViewToClipMatrix = &args.ViewToClip,
                        PreExposure = args.PreExposure,
                        ExposureScale = args.ExposureScale,
                        MotionVectorsReflections = args.SpecularMotionVectors?.DirectX12 ?? 0,
                        TransparencyLayer = args.TransparencyLayer?.DirectX12 ?? 0,
                        TransparencyLayerOpacity = args.TransparencyLayerOpacity?.DirectX12 ?? 0
                    };

                    Ngx.D3D12.EvaluateDLSSDExt(commandList, handle, parameters, &evalParams).Success();
                }
                break;

            case GraphicsApi.Vulkan:
                {
                    NGXResourceVK diffuseAlbedo = args.DiffuseAlbedo.Vulkan with { ReadWrite = false };
                    NGXResourceVK specularAlbedo = args.SpecularAlbedo.Vulkan with { ReadWrite = false };
                    NGXResourceVK normals = args.Normals.Vulkan with { ReadWrite = false };
                    NGXResourceVK roughness = (args.Roughness?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK input = args.Input.Vulkan with { ReadWrite = false };
                    NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };
                    NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
                    NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
                    NGXResourceVK exposure = (args.Exposure?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK reactiveMask = (args.ReactiveMask?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK colorBeforeTransparency = (args.ColorBeforeTransparency?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK subsurfaceScatteringGuide = (args.SubsurfaceScatteringGuide?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK depthOfFieldGuide = (args.DepthOfFieldGuide?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK specularHitDistance = (args.SpecularHitDistance?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK specularMotionVectors = (args.SpecularMotionVectors?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK transparencyLayer = (args.TransparencyLayer?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK transparencyLayerOpacity = (args.TransparencyLayerOpacity?.Vulkan ?? default) with { ReadWrite = false };

                    NGXVKDLSSDEvalParams evalParams = new()
                    {
                        DiffuseAlbedo = &diffuseAlbedo,
                        SpecularAlbedo = &specularAlbedo,
                        Normals = &normals,
                        Roughness = args.Roughness.HasValue ? &roughness : null,
                        Color = &input,
                        Output = &output,
                        Depth = &depth,
                        MotionVectors = &motionVectors,
                        JitterOffsetX = args.JitterOffsetX,
                        JitterOffsetY = args.JitterOffsetY,
                        RenderSubrectDimensions = renderSubrectDimensions,
                        Reset = args.Reset ? 1 : 0,
                        MVScaleX = args.MotionVectorScaleX,
                        MVScaleY = args.MotionVectorScaleY,
                        ExposureTexture = args.Exposure.HasValue ? &exposure : null,
                        BiasCurrentColorMask = args.ReactiveMask.HasValue ? &reactiveMask : null,
                        PreExposure = args.PreExposure,
                        ExposureScale = args.ExposureScale,
                        ColorBeforeTransparency = args.ColorBeforeTransparency.HasValue ? &colorBeforeTransparency : null,
                        ScreenSpaceSubsurfaceScatteringGuide = args.SubsurfaceScatteringGuide.HasValue ? &subsurfaceScatteringGuide : null,
                        DepthOfFieldGuide = args.DepthOfFieldGuide.HasValue ? &depthOfFieldGuide : null,
                        SpecularHitDistance = args.SpecularHitDistance.HasValue ? &specularHitDistance : null,
                        WorldToViewMatrix = &args.WorldToView,
                        ViewToClipMatrix = &args.ViewToClip,
                        MotionVectorsReflections = args.SpecularMotionVectors.HasValue ? &specularMotionVectors : null,
                        TransparencyLayer = args.TransparencyLayer.HasValue ? &transparencyLayer : null,
                        TransparencyLayerOpacity = args.TransparencyLayerOpacity.HasValue ? &transparencyLayerOpacity : null
                    };

                    Ngx.Vulkan.EvaluateDLSSDExt(commandList, handle, parameters, &evalParams).Success();
                }
                break;
        }
    }
}
