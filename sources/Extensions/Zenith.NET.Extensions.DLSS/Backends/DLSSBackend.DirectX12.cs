using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend
{
    private void InitializeDirectX12(byte* projectId, byte* engineVersion, void* dataPath, NGXFeatureCommonInfo* featureInfo)
    {
        Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, Device, featureInfo, NGXVersion.Api).Success();
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

    private static void EvaluateDirectX12(nint commandList, DLSSSuperResolution feature, DLSSSuperResolutionArgs args, NGXDimensions renderSubrectDimensions)
    {
        NGXD3D12DLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = args.Color.DirectX12,
                Output = args.Output.DirectX12
            },
            Depth = args.Depth.DirectX12,
            MotionVectors = args.MotionVectors.DirectX12,
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            RenderSubrectDimensions = renderSubrectDimensions,
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            ExposureTexture = args.Exposure?.DirectX12 ?? 0,
            BiasCurrentColorMask = args.BiasCurrentColorMask?.DirectX12 ?? 0,
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
            Color = args.Color.DirectX12,
            Alpha = args.Alpha?.DirectX12 ?? 0,
            Output = args.Output.DirectX12,
            OutputAlpha = args.OutputAlpha?.DirectX12 ?? 0,
            Depth = args.Depth.DirectX12,
            MotionVectors = args.MotionVectors.DirectX12,
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            RenderSubrectDimensions = renderSubrectDimensions,
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            ExposureTexture = args.Exposure?.DirectX12 ?? 0,
            BiasCurrentColorMask = args.BiasCurrentColorMask?.DirectX12 ?? 0,
            ReflectedAlbedo = args.ReflectedAlbedo?.DirectX12 ?? 0,
            ColorBeforeParticles = args.ColorBeforeParticles?.DirectX12 ?? 0,
            ColorAfterParticles = args.ColorAfterParticles?.DirectX12 ?? 0,
            ColorBeforeTransparency = args.ColorBeforeTransparency?.DirectX12 ?? 0,
            ColorAfterTransparency = args.ColorAfterTransparency?.DirectX12 ?? 0,
            ColorBeforeFog = args.ColorBeforeFog?.DirectX12 ?? 0,
            ColorAfterFog = args.ColorAfterFog?.DirectX12 ?? 0,
            ScreenSpaceSubsurfaceScatteringGuide = args.ScreenSpaceSubsurfaceScatteringGuide?.DirectX12 ?? 0,
            ColorBeforeScreenSpaceSubsurfaceScattering = args.ColorBeforeScreenSpaceSubsurfaceScattering?.DirectX12 ?? 0,
            ColorAfterScreenSpaceSubsurfaceScattering = args.ColorAfterScreenSpaceSubsurfaceScattering?.DirectX12 ?? 0,
            ScreenSpaceRefractionGuide = args.ScreenSpaceRefractionGuide?.DirectX12 ?? 0,
            ColorBeforeScreenSpaceRefraction = args.ColorBeforeScreenSpaceRefraction?.DirectX12 ?? 0,
            ColorAfterScreenSpaceRefraction = args.ColorAfterScreenSpaceRefraction?.DirectX12 ?? 0,
            DepthOfFieldGuide = args.DepthOfFieldGuide?.DirectX12 ?? 0,
            ColorBeforeDepthOfField = args.ColorBeforeDepthOfField?.DirectX12 ?? 0,
            ColorAfterDepthOfField = args.ColorAfterDepthOfField?.DirectX12 ?? 0,
            DiffuseHitDistance = args.DiffuseHitDistance?.DirectX12 ?? 0,
            SpecularHitDistance = args.SpecularHitDistance?.DirectX12 ?? 0,
            DiffuseRayDirection = args.DiffuseRayDirection?.DirectX12 ?? 0,
            SpecularRayDirection = args.SpecularRayDirection?.DirectX12 ?? 0,
            DiffuseRayDirectionHitDistance = args.DiffuseRayDirectionHitDistance?.DirectX12 ?? 0,
            SpecularRayDirectionHitDistance = args.SpecularRayDirectionHitDistance?.DirectX12 ?? 0,
            WorldToViewMatrix = &args.WorldToView,
            ViewToClipMatrix = &args.ViewToClip,
            PreExposure = args.PreExposure,
            ExposureScale = args.ExposureScale,
            MotionVectorsReflections = args.SpecularMotionVectors?.DirectX12 ?? 0,
            TransparencyLayer = args.TransparencyLayer?.DirectX12 ?? 0,
            TransparencyLayerOpacity = args.TransparencyLayerOpacity?.DirectX12 ?? 0,
            TransparencyLayerMvecs = args.TransparencyLayerMotionVectors?.DirectX12 ?? 0,
            DisocclusionMask = args.DisocclusionMask?.DirectX12 ?? 0,
            ResponsivityMask = args.ResponsivityMask?.DirectX12 ?? 0
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
            OutputInterpFrame = args.Output.DirectX12,
            OutputDisableInterpolation = args.OutputDisableInterpolation?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0
        };

        Ngx.D3D12.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams).Success();
    }

    private static DLSSBinding CreateDirectX12Binding(Texture texture, NGXDimensions size)
    {
        return new(texture.GetNativeObject(NativeObjectType.D3D12Resource), default, size);
    }
}
