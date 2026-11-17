using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor
{
    private bool InitializeDirectX12(string projectId, string engineVersion, string dataPath, NGXFeatureCommonInfo* commonInfo)
    {
        Device = Context.DirectX12();

        return Ngx.NVSDK_NGX_SUCCEED(Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, Device, commonInfo, NGXVersion.Api));
    }

    private void ShutdownDirectX12()
    {
        Ngx.D3D12.Shutdown1(Device).Success();
    }

    private static void CreateDirectX12(nint commandList, DLSSSuperResolution feature, NGXDLSSCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private static void CreateDirectX12(nint commandList, DLSSRayReconstruction feature, NGXDLSSDCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSDExt(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private static void CreateDirectX12(nint commandList, DLSSFrameGeneration feature, NGXDLSSGCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSG(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private static void EvaluateDirectX12(nint commandList, DLSSSuperResolution feature, SuperResolutionArgs args)
    {
        NGXD3D12DLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = args.Color.DirectX12(),
                Output = feature.Output.DirectX12()
            },
            Depth = args.Depth.DirectX12(),
            MotionVectors = args.MotionVectors.DirectX12(),
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = feature.Desc.RenderWidth,
                Height = feature.Desc.RenderHeight
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = feature.Desc.RenderWidth,
            MVScaleY = feature.Desc.RenderHeight,
            ExposureTexture = args.Exposure?.DirectX12() ?? 0,
            BiasCurrentColorMask = args.ReactiveMask?.DirectX12() ?? 0,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f
        };

        Ngx.D3D12.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private static void EvaluateDirectX12(nint commandList, DLSSRayReconstruction feature, RayReconstructionArgs args)
    {
        NGXD3D12DLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = args.DiffuseAlbedo.DirectX12(),
            SpecularAlbedo = args.SpecularAlbedo.DirectX12(),
            Normals = args.Normals.DirectX12(),
            Roughness = args.Roughness.DirectX12(),
            Color = args.Color.DirectX12(),
            Output = feature.Output.DirectX12(),
            Depth = args.Depth.DirectX12(),
            MotionVectors = args.MotionVectors.DirectX12(),
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = feature.Desc.RenderWidth,
                Height = feature.Desc.RenderHeight
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = feature.Desc.RenderWidth,
            MVScaleY = feature.Desc.RenderHeight,
            ExposureTexture = args.Exposure?.DirectX12() ?? 0,
            BiasCurrentColorMask = args.ReactiveMask?.DirectX12() ?? 0,
            SpecularHitDistance = args.SpecularHitDistance?.DirectX12() ?? 0,
            WorldToViewMatrix = &args.View,
            ViewToClipMatrix = &args.Projection,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            TransparencyLayer = args.TransparencyOverlay?.DirectX12() ?? 0
        };

        Ngx.D3D12.EvaluateDLSSDExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private static void EvaluateDirectX12(nint commandList, DLSSFrameGeneration feature, FrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        NGXD3D12DLSSGEvalParams evalParams = new()
        {
            Backbuffer = args.Color.DirectX12(),
            Depth = args.Depth.DirectX12(),
            MVecs = args.MotionVectors.DirectX12(),
            Hudless = args.Hudless?.DirectX12() ?? 0,
            OutputInterpFrame = feature.Output.DirectX12()
        };

        Ngx.D3D12.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams).Success();
    }
}
