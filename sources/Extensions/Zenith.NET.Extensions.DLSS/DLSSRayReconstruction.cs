using NGX.NET;
using Ngx = NGX.NET.NGX;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSRayReconstruction : DisposableObject
{
    private readonly DLSSContext dlssContext;
    private readonly NGXParameter* parameters;

    private NGXHandle* handle;

    internal DLSSRayReconstruction(DLSSContext dlssContext, DLSSRayReconstructionDesc desc)
    {
        this.dlssContext = dlssContext;

        parameters = dlssContext.AllocateParameters();

        Desc = desc;
    }

    public DLSSRayReconstructionDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSRayReconstructionArgs args)
    {
        if (!dlssContext.IsInitialized)
        {
            return;
        }

        using Lock.Scope _ = DLSSContext.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Ray Reconstruction");
        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);

        nint commandList = dlssContext.CommandList(commandBuffer);

        dlssContext.BindEmptyDescriptorSet(commandList);
        dlssContext.RemoveDestroyedBindings();

        if (handle is null)
        {
            Create(commandList);
        }

        if (handle is not null)
        {
            Evaluate(commandList, args);
        }

        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);
        commandBuffer.EndDebugEvent();
    }

    protected override void Destroy()
    {
        if (handle is not null)
        {
            dlssContext.ReleaseFeature(handle);
        }

        dlssContext.DestroyParameters(parameters);

        Extensions.ReleaseContext(dlssContext);
    }

    private void Create(nint commandList)
    {
        NGXDLSSDCreateParams createParams = new()
        {
            InDenoiseMode = NGXDLSSDenoiseMode.DLUnified,
            InRoughnessMode = DLSSFormats.RoughnessMode(Desc),
            InUseHWDepth = DLSSFormats.DepthType(Desc),
            InWidth = Desc.InputWidth,
            InHeight = Desc.InputHeight,
            InTargetWidth = Desc.OutputWidth,
            InTargetHeight = Desc.OutputHeight,
            InPerfQualityValue = DLSSFormats.PerfQualityValue(Desc.Mode),
            InFeatureCreateFlags = (int)DLSSFormats.FeatureFlags(Desc)
        };

        NGXHandle* createdHandle = null;

        NGXResult result = dlssContext.IsVulkan ? Ngx.Vulkan.CreateDLSSDExt1(dlssContext.Device, commandList, 1, 1, &createdHandle, parameters, &createParams) : Ngx.D3D12.CreateDLSSDExt(commandList, 1, 1, &createdHandle, parameters, &createParams);

        result.Success();

        if (Ngx.Succeeded(result))
        {
            handle = createdHandle;
        }
    }

    private void Evaluate(nint commandList, DLSSRayReconstructionArgs args)
    {
        NGXDimensions renderSubrectDimensions = new() { Width = Desc.InputWidth, Height = Desc.InputHeight };

        if (dlssContext.IsVulkan)
        {
            NGXResourceVK* resources = stackalloc NGXResourceVK[17];

            NGXVKDLSSDEvalParams evalParams = new()
            {
                PInDiffuseAlbedo = dlssContext.VulkanResource(&resources[0], args.DiffuseAlbedo, false),
                PInSpecularAlbedo = dlssContext.VulkanResource(&resources[1], args.SpecularAlbedo, false),
                PInNormals = dlssContext.VulkanResource(&resources[2], args.Normals, false),
                PInRoughness = dlssContext.VulkanResource(&resources[3], args.Roughness, false),
                PInColor = dlssContext.VulkanResource(&resources[4], args.Input, false),
                PInOutput = dlssContext.VulkanResource(&resources[5], args.Output, true),
                PInDepth = dlssContext.VulkanResource(&resources[6], args.Depth, false),
                PInMotionVectors = dlssContext.VulkanResource(&resources[7], args.MotionVectors, false),
                InJitterOffsetX = args.JitterOffsetX,
                InJitterOffsetY = args.JitterOffsetY,
                InRenderSubrectDimensions = renderSubrectDimensions,
                InReset = args.Reset ? 1 : 0,
                InMVScaleX = args.MotionVectorScaleX,
                InMVScaleY = args.MotionVectorScaleY,
                PInExposureTexture = dlssContext.VulkanResource(&resources[8], args.Exposure, false),
                PInBiasCurrentColorMask = dlssContext.VulkanResource(&resources[9], args.ReactiveMask, false),
                PInColorBeforeTransparency = dlssContext.VulkanResource(&resources[10], args.ColorBeforeTransparency, false),
                PInScreenSpaceSubsurfaceScatteringGuide = dlssContext.VulkanResource(&resources[11], args.SubsurfaceScatteringGuide, false),
                PInDepthOfFieldGuide = dlssContext.VulkanResource(&resources[12], args.DepthOfFieldGuide, false),
                PInSpecularHitDistance = dlssContext.VulkanResource(&resources[13], args.SpecularHitDistance, false),
                PInWorldToViewMatrix = &args.WorldToView,
                PInViewToClipMatrix = &args.ViewToClip,
                InPreExposure = args.PreExposure,
                InExposureScale = args.ExposureScale,
                PInMotionVectorsReflections = dlssContext.VulkanResource(&resources[14], args.SpecularMotionVectors, false),
                PInTransparencyLayer = dlssContext.VulkanResource(&resources[15], args.TransparencyLayer, false),
                PInTransparencyLayerOpacity = dlssContext.VulkanResource(&resources[16], args.TransparencyLayerOpacity, false)
            };

            Ngx.Vulkan.EvaluateDLSSDExt(commandList, handle, parameters, &evalParams).Success();
        }
        else
        {
            NGXD3D12DLSSDEvalParams evalParams = new()
            {
                PInDiffuseAlbedo = DLSSContext.D3D12Resource(args.DiffuseAlbedo),
                PInSpecularAlbedo = DLSSContext.D3D12Resource(args.SpecularAlbedo),
                PInNormals = DLSSContext.D3D12Resource(args.Normals),
                PInRoughness = DLSSContext.D3D12Resource(args.Roughness),
                PInColor = DLSSContext.D3D12Resource(args.Input),
                PInOutput = DLSSContext.D3D12Resource(args.Output),
                PInDepth = DLSSContext.D3D12Resource(args.Depth),
                PInMotionVectors = DLSSContext.D3D12Resource(args.MotionVectors),
                InJitterOffsetX = args.JitterOffsetX,
                InJitterOffsetY = args.JitterOffsetY,
                InRenderSubrectDimensions = renderSubrectDimensions,
                InReset = args.Reset ? 1 : 0,
                InMVScaleX = args.MotionVectorScaleX,
                InMVScaleY = args.MotionVectorScaleY,
                PInExposureTexture = DLSSContext.D3D12Resource(args.Exposure),
                PInBiasCurrentColorMask = DLSSContext.D3D12Resource(args.ReactiveMask),
                PInColorBeforeTransparency = DLSSContext.D3D12Resource(args.ColorBeforeTransparency),
                PInScreenSpaceSubsurfaceScatteringGuide = DLSSContext.D3D12Resource(args.SubsurfaceScatteringGuide),
                PInDepthOfFieldGuide = DLSSContext.D3D12Resource(args.DepthOfFieldGuide),
                PInSpecularHitDistance = DLSSContext.D3D12Resource(args.SpecularHitDistance),
                PInWorldToViewMatrix = &args.WorldToView,
                PInViewToClipMatrix = &args.ViewToClip,
                InPreExposure = args.PreExposure,
                InExposureScale = args.ExposureScale,
                PInMotionVectorsReflections = DLSSContext.D3D12Resource(args.SpecularMotionVectors),
                PInTransparencyLayer = DLSSContext.D3D12Resource(args.TransparencyLayer),
                PInTransparencyLayerOpacity = DLSSContext.D3D12Resource(args.TransparencyLayerOpacity)
            };

            Ngx.D3D12.EvaluateDLSSDExt(commandList, handle, parameters, &evalParams).Success();
        }
    }
}
