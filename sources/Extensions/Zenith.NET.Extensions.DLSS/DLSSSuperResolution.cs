using NGX.NET;
using Ngx = NGX.NET.NGX;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSSuperResolution : DisposableObject
{
    private readonly DLSSContext dlssContext;
    private readonly NGXParameter* parameters;

    private NGXHandle* handle;

    internal DLSSSuperResolution(DLSSContext dlssContext, DLSSSuperResolutionDesc desc)
    {
        this.dlssContext = dlssContext;

        parameters = dlssContext.AllocateParameters();

        Desc = desc;
    }

    public DLSSSuperResolutionDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSSuperResolutionArgs args)
    {
        if (!dlssContext.IsInitialized)
        {
            return;
        }

        using Lock.Scope _ = DLSSContext.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Super Resolution");
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
        NGXDLSSCreateParams createParams = new()
        {
            Feature = new()
            {
                InWidth = Desc.InputWidth,
                InHeight = Desc.InputHeight,
                InTargetWidth = Desc.OutputWidth,
                InTargetHeight = Desc.OutputHeight,
                InPerfQualityValue = DLSSFormats.PerfQualityValue(Desc.Mode)
            },
            InFeatureCreateFlags = (int)DLSSFormats.FeatureFlags(Desc)
        };

        NGXHandle* createdHandle = null;

        NGXResult result = dlssContext.IsVulkan ? Ngx.Vulkan.CreateDLSSExt1(dlssContext.Device, commandList, 1, 1, &createdHandle, parameters, &createParams) : Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, &createdHandle, parameters, &createParams);

        result.Success();

        if (Ngx.Succeeded(result))
        {
            handle = createdHandle;
        }
    }

    private void Evaluate(nint commandList, DLSSSuperResolutionArgs args)
    {
        NGXDimensions renderSubrectDimensions = new() { Width = args.InputContentWidth, Height = args.InputContentHeight };

        if (dlssContext.IsVulkan)
        {
            NGXResourceVK* resources = stackalloc NGXResourceVK[6];

            NGXVKDLSSEvalParams evalParams = new()
            {
                Feature = new()
                {
                    PInColor = dlssContext.VulkanResource(&resources[0], args.Input, false),
                    PInOutput = dlssContext.VulkanResource(&resources[1], args.Output, true)
                },
                PInDepth = dlssContext.VulkanResource(&resources[2], args.Depth, false),
                PInMotionVectors = dlssContext.VulkanResource(&resources[3], args.MotionVectors, false),
                InJitterOffsetX = args.JitterOffsetX,
                InJitterOffsetY = args.JitterOffsetY,
                InRenderSubrectDimensions = renderSubrectDimensions,
                InReset = args.Reset ? 1 : 0,
                InMVScaleX = args.MotionVectorScaleX,
                InMVScaleY = args.MotionVectorScaleY,
                PInExposureTexture = dlssContext.VulkanResource(&resources[4], args.Exposure, false),
                PInBiasCurrentColorMask = dlssContext.VulkanResource(&resources[5], args.ReactiveMask, false),
                InPreExposure = args.PreExposure,
                InExposureScale = args.ExposureScale
            };

            Ngx.Vulkan.EvaluateDLSSExt(commandList, handle, parameters, &evalParams).Success();
        }
        else
        {
            NGXD3D12DLSSEvalParams evalParams = new()
            {
                Feature = new()
                {
                    PInColor = DLSSContext.D3D12Resource(args.Input),
                    PInOutput = DLSSContext.D3D12Resource(args.Output)
                },
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
                InPreExposure = args.PreExposure,
                InExposureScale = args.ExposureScale
            };

            Ngx.D3D12.EvaluateDLSSExt(commandList, handle, parameters, &evalParams).Success();
        }
    }
}
