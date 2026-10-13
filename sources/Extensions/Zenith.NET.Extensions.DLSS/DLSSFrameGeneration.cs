using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSFrameGeneration : DisposableObject
{
    private readonly DLSSBackend backend;
    private readonly NGXParameter parameters;

    private NGXHandle handle;

    internal DLSSFrameGeneration(DLSSBackend backend, DLSSFrameGenerationDesc desc)
    {
        this.backend = backend;

        parameters = backend.AllocateParameters();

        backend.SetUI(parameters, Ngx.DLSSGParameterMvecJittered, desc.IsMotionVectorJittered ? 1u : 0u);
        backend.SetUI(parameters, Ngx.DLSSGParameterUserInterfaceRecompositionEnabled, desc.IsUIRecompositionEnabled ? 1u : 0u);

        Desc = desc;
    }

    public DLSSFrameGenerationDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSFrameGenerationArgs args)
    {
        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Frame Generation");
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
        NGXDLSSGCreateParams createParams = new()
        {
            Width = Desc.OutputWidth,
            Height = Desc.OutputHeight,
            NativeBackbufferFormat = backend.Context.GraphicsApi switch
            {
                GraphicsApi.DirectX12 => DLSSFormats.DirectX12(Desc.Format),
                GraphicsApi.Vulkan => DLSSFormats.Vulkan(Desc.Format).Format,
                _ => default
            },
            RenderWidth = Desc.InputWidth,
            RenderHeight = Desc.InputHeight,
            DynamicResolutionScaling = Desc.IsDynamicResolutionEnabled
        };

        fixed (NGXHandle* pHandle = &handle)
        {
            switch (backend.Context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    Ngx.D3D12.CreateDLSSG(commandList, 1, 1, pHandle, parameters, &createParams).Success();
                    break;

                case GraphicsApi.Vulkan:
                    Ngx.Vulkan.CreateDLSSG(commandList, 1, 1, pHandle, parameters, &createParams).Success();
                    break;
            }
        }
    }

    private void Evaluate(nint commandList, DLSSFrameGenerationArgs args)
    {
        Matrix4x4.Invert(args.ViewToClip, out Matrix4x4 clipToView);
        Matrix4x4.Invert(args.ClipToPrevClip, out Matrix4x4 prevClipToClip);

        NGXDimensions inputContentSize = new()
        {
            Width = args.InputContentWidth,
            Height = args.InputContentHeight
        };

        NGXDLSSGOptEvalParams optEvalParams = new()
        {
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
            HudLessSubrectSize = args.HudlessColor?.Size ?? default,
            UiSubrectSize = args.UI?.Size ?? default,
            UiAlphaSubrectSize = args.UIAlpha?.Size ?? default,
            BidirectionalDistFieldSubrectSize = args.DistortionField?.Size ?? default,
            BackbufferSubrectSize = args.Color.Size,
            OutputInterpSubrectSize = args.Output.Size
        };

        switch (backend.Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                {
                    NGXD3D12DLSSGEvalParams evalParams = new()
                    {
                        Backbuffer = args.Color.DirectX12,
                        Depth = args.Depth.DirectX12,
                        MVecs = args.MotionVectors.DirectX12,
                        Hudless = args.HudlessColor?.DirectX12 ?? 0,
                        UI = args.UI?.DirectX12 ?? 0,
                        UIAlpha = args.UIAlpha?.DirectX12 ?? 0,
                        BidirectionalDistortionField = args.DistortionField?.DirectX12 ?? 0,
                        OutputInterpFrame = args.Output.DirectX12
                    };

                    Ngx.D3D12.EvaluateDLSSG(commandList, handle, parameters, &evalParams, &optEvalParams).Success();
                }
                break;

            case GraphicsApi.Vulkan:
                {
                    NGXResourceVK color = args.Color.Vulkan with { ReadWrite = false };
                    NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
                    NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
                    NGXResourceVK hudlessColor = (args.HudlessColor?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK ui = (args.UI?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK uiAlpha = (args.UIAlpha?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK distortionField = (args.DistortionField?.Vulkan ?? default) with { ReadWrite = false };
                    NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };

                    NGXVKDLSSGEvalParams evalParams = new()
                    {
                        Backbuffer = &color,
                        Depth = &depth,
                        MVecs = &motionVectors,
                        Hudless = args.HudlessColor.HasValue ? &hudlessColor : null,
                        UI = args.UI.HasValue ? &ui : null,
                        UIAlpha = args.UIAlpha.HasValue ? &uiAlpha : null,
                        BidirectionalDistortionField = args.DistortionField.HasValue ? &distortionField : null,
                        OutputInterpFrame = &output
                    };

                    Ngx.Vulkan.EvaluateDLSSG(commandList, handle, parameters, &evalParams, &optEvalParams).Success();
                }
                break;
        }
    }
}
