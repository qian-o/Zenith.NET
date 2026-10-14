using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend
{
    private void InitializeDirectX12(byte* projectId, byte* engineVersion, void* dataPath, NGXFeatureCommonInfo* featureInfo)
    {
        Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, Device, featureInfo, NGXVersion.Api).Success();
    }

    private void CreateDirectX12(nint commandList, DLSSSuperResolution feature, NGXDLSSCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateDirectX12(nint commandList, DLSSRayReconstruction feature, NGXDLSSDCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSDExt(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateDirectX12(nint commandList, DLSSFrameGeneration feature, NGXDLSSGCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSG(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private static void EvaluateDirectX12(nint commandList, DLSSSuperResolution feature, DLSSSuperResolutionArgs args, NGXDimensions renderSubrectDimensions)
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

        Ngx.D3D12.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private static void EvaluateDirectX12(nint commandList, DLSSRayReconstruction feature, DLSSRayReconstructionArgs args, NGXDimensions renderSubrectDimensions)
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

        Ngx.D3D12.EvaluateDLSSDExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private static void EvaluateDirectX12(nint commandList, DLSSFrameGeneration feature, DLSSFrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
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

        Ngx.D3D12.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams).Success();
    }

    private static DLSSBinding CreateDirectX12Binding(Texture texture, NGXDimensions size)
    {
        return new(texture.GetNativeObject(NativeObjectType.D3D12Resource), default, size);
    }

    private void ShutdownDirectX12()
    {
        Ngx.D3D12.Shutdown1(Device).Success();
    }
}
