using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSSuperResolution : DisposableObject
{
    private readonly DLSSBackend backend;
    private readonly NGXParameter parameters;

    private NGXHandle handle;

    internal DLSSSuperResolution(DLSSBackend backend, DLSSSuperResolutionDesc desc)
    {
        this.backend = backend;

        parameters = backend.AllocateParameters();

        Desc = desc;
    }

    public DLSSSuperResolutionDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSSuperResolutionArgs args)
    {
        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Super Resolution");
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
        NGXDLSSCreateParams createParams = new()
        {
            Feature = new()
            {
                Width = Desc.InputWidth,
                Height = Desc.InputHeight,
                TargetWidth = Desc.OutputWidth,
                TargetHeight = Desc.OutputHeight,
                PerfQualityValue = DLSSFormats.PerfQualityValue(Desc.Mode)
            },
            FeatureCreateFlags = (int)DLSSFormats.FeatureFlags(Desc)
        };

        fixed (NGXHandle* pHandle = &handle)
        {
            switch (backend.Context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, pHandle, parameters, &createParams).Success();
                    break;

                case GraphicsApi.Vulkan:
                    Ngx.Vulkan.CreateDLSSExt1(backend.Device, commandList, 1, 1, pHandle, parameters, &createParams).Success();
                    break;
            }
        }
    }

    private void Evaluate(nint commandList, DLSSSuperResolutionArgs args)
    {
        NGXDimensions renderSubrectDimensions = new()
        {
            Width = args.InputContentWidth,
            Height = args.InputContentHeight
        };

        switch (backend.Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                {
                    NGXD3D12DLSSEvalParams evalParams = new()
                    {
                        Feature = new()
                        {
                            Color = args.Input.DirectX12,
                            Output = args.Output.DirectX12
                        },
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
                        PreExposure = args.PreExposure,
                        ExposureScale = args.ExposureScale
                    };

                    Ngx.D3D12.EvaluateDLSSExt(commandList, handle, parameters, &evalParams).Success();
                }
                break;

            case GraphicsApi.Vulkan:
                {
                    NGXResourceVK input = args.Input.Vulkan with { ReadWrite = false };
                    NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };
                    NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
                    NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
                    NGXResourceVK exposure = (args.Exposure?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK reactiveMask = (args.ReactiveMask?.Vulkan ?? default) with { ReadWrite = false };

                    NGXVKDLSSEvalParams evalParams = new()
                    {
                        Feature = new()
                        {
                            Color = &input,
                            Output = &output
                        },
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
                        ExposureScale = args.ExposureScale
                    };

                    Ngx.Vulkan.EvaluateDLSSExt(commandList, handle, parameters, &evalParams).Success();
                }
                break;
        }
    }
}
