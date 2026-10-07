using System.Numerics;
using NGX.NET;
using Ngx = NGX.NET.NGX;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSFrameGeneration : DisposableObject
{
    private readonly DLSSContext dlssContext;
    private readonly NGXParameter* parameters;

    private NGXHandle* handle;

    internal DLSSFrameGeneration(DLSSContext dlssContext, DLSSFrameGenerationDesc desc)
    {
        this.dlssContext = dlssContext;

        parameters = dlssContext.AllocateParameters();

        dlssContext.SetUI(parameters, Ngx.DLSSGParameterMvecJittered, desc.IsMotionVectorJittered ? 1u : 0u);
        dlssContext.SetUI(parameters, Ngx.DLSSGParameterUserInterfaceRecompositionEnabled, desc.IsUIRecompositionEnabled ? 1u : 0u);

        Desc = desc;
    }

    public DLSSFrameGenerationDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSFrameGenerationArgs args)
    {
        if (!dlssContext.IsInitialized)
        {
            return;
        }

        using Lock.Scope _ = DLSSContext.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Frame Generation");
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
        NGXDLSSGCreateParams createParams = new()
        {
            Width = Desc.OutputWidth,
            Height = Desc.OutputHeight,
            NativeBackbufferFormat = dlssContext.IsVulkan ? DLSSFormats.Vulkan(Desc.Format).Format : DLSSFormats.DirectX12(Desc.Format),
            RenderWidth = Desc.InputWidth,
            RenderHeight = Desc.InputHeight,
            DynamicResolutionScaling = Desc.IsDynamicResolutionEnabled
        };

        NGXHandle* createdHandle = null;

        NGXResult result = dlssContext.IsVulkan ? Ngx.Vulkan.CreateDLSSG(commandList, 1, 1, &createdHandle, parameters, &createParams) : Ngx.D3D12.CreateDLSSG(commandList, 1, 1, &createdHandle, parameters, &createParams);

        result.Success();

        if (Ngx.Succeeded(result))
        {
            handle = createdHandle;
        }
    }

    private void Evaluate(nint commandList, DLSSFrameGenerationArgs args)
    {
        Matrix4x4.Invert(args.ViewToClip, out Matrix4x4 clipToView);
        Matrix4x4.Invert(args.ClipToPrevClip, out Matrix4x4 prevClipToClip);

        NGXDimensions inputContentSize = new() { Width = args.InputContentWidth, Height = args.InputContentHeight };

        NGXDLSSGOptEvalParams optEvalParams = new()
        {
            MultiFrameCount = 1,
            MultiFrameIndex = 1,
            CameraViewToClip = args.ViewToClip,
            ClipToCameraView = clipToView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = args.ClipToPrevClip,
            PrevClipToClip = prevClipToClip,
            JitterOffset = new(args.JitterOffsetX, args.JitterOffsetY),
            MvecScale = new(args.MotionVectorScaleX, args.MotionVectorScaleY),
            CameraPinholeOffset = new(args.CameraPinholeOffsetX, args.CameraPinholeOffsetY),
            CameraPos = args.CameraPosition,
            CameraUp = args.CameraUp,
            CameraRight = args.CameraRight,
            CameraFwd = args.CameraForward,
            CameraNear = args.CameraNear,
            CameraFar = args.CameraFar,
            CameraFOV = args.CameraFovAngleVer,
            CameraAspectRatio = args.CameraAspectRatio,
            ColorBuffersHDR = Desc.IsHdr,
            DepthInverted = Desc.IsDepthReversed,
            CameraMotionIncluded = true,
            Reset = args.Reset,
            OrthoProjection = args.Orthographic,
            MvecsSubrectSize = inputContentSize,
            DepthSubrectSize = inputContentSize,
            HudLessSubrectSize = Size(args.HudlessColor),
            UiSubrectSize = Size(args.UI),
            UiAlphaSubrectSize = Size(args.UIAlpha),
            BidirectionalDistFieldSubrectSize = Size(args.DistortionField),
            MinRelativeLinearDepthObjectSeparation = 40.0f,
            BackbufferSubrectSize = Size(args.Color),
            OutputInterpSubrectSize = Size(args.Output)
        };

        if (dlssContext.IsVulkan)
        {
            NGXResourceVK* resources = stackalloc NGXResourceVK[8];

            NGXVKDLSSGEvalParams evalParams = new()
            {
                PBackbuffer = dlssContext.VulkanResource(&resources[0], args.Color, false),
                PDepth = dlssContext.VulkanResource(&resources[1], args.Depth, false),
                PMVecs = dlssContext.VulkanResource(&resources[2], args.MotionVectors, false),
                PHudless = dlssContext.VulkanResource(&resources[3], args.HudlessColor, false),
                PUI = dlssContext.VulkanResource(&resources[4], args.UI, false),
                PUIAlpha = dlssContext.VulkanResource(&resources[5], args.UIAlpha, false),
                PBidirectionalDistortionField = dlssContext.VulkanResource(&resources[6], args.DistortionField, false),
                POutputInterpFrame = dlssContext.VulkanResource(&resources[7], args.Output, true)
            };

            Ngx.Vulkan.EvaluateDLSSG(commandList, handle, parameters, &evalParams, &optEvalParams).Success();
        }
        else
        {
            NGXD3D12DLSSGEvalParams evalParams = new()
            {
                PBackbuffer = DLSSContext.D3D12Resource(args.Color),
                PDepth = DLSSContext.D3D12Resource(args.Depth),
                PMVecs = DLSSContext.D3D12Resource(args.MotionVectors),
                PHudless = DLSSContext.D3D12Resource(args.HudlessColor),
                PUI = DLSSContext.D3D12Resource(args.UI),
                PUIAlpha = DLSSContext.D3D12Resource(args.UIAlpha),
                PBidirectionalDistortionField = DLSSContext.D3D12Resource(args.DistortionField),
                POutputInterpFrame = DLSSContext.D3D12Resource(args.Output)
            };

            Ngx.D3D12.EvaluateDLSSG(commandList, handle, parameters, &evalParams, &optEvalParams).Success();
        }
    }

    private static NGXDimensions Size(DLSSBinding binding)
    {
        return binding.Texture is Texture texture ? new() { Width = texture.Desc.Width, Height = texture.Desc.Height } : default;
    }
}
