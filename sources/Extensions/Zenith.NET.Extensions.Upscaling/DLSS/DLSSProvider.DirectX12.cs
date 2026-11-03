using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSProvider
{
    private NGXResult InitializeDirectX12(byte* projectId, byte* engineVersion, void* dataPath, NGXFeatureCommonInfo* featureInfo)
    {
        return Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, Device, featureInfo, NGXVersion.Api);
    }

    private void ShutdownDirectX12()
    {
        Ngx.D3D12.Shutdown1(Device).Success();
    }

    private static void CreateDirectX12(nint commandList, DLSSFeature feature, NGXDLSSCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private static void CreateDirectX12(nint commandList, DLSSFeature feature, NGXDLSSDCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSDExt(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private static void CreateDirectX12(nint commandList, DLSSFeature feature, NGXDLSSGCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.D3D12.CreateDLSSG(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private static void EvaluateDirectX12(nint commandList, DLSSFeature feature, TemporalUpscalerArgs args)
    {
        NGXD3D12DLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = args.Color.GetNativeObject(NativeObjectType.D3D12Resource),
                Output = args.Output.GetNativeObject(NativeObjectType.D3D12Resource)
            },
            Depth = args.Depth.GetNativeObject(NativeObjectType.D3D12Resource),
            MotionVectors = args.MotionVectors.GetNativeObject(NativeObjectType.D3D12Resource),
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            ExposureTexture = args.Exposure?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            BiasCurrentColorMask = args.ReactiveMask?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            PreExposure = args.PreExposure,
            ExposureScale = 1.0f
        };

        Ngx.D3D12.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private static void EvaluateDirectX12(nint commandList, DLSSFeature feature, DenoisingUpscalerArgs args)
    {
        NGXD3D12DLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = args.DiffuseAlbedo.GetNativeObject(NativeObjectType.D3D12Resource),
            SpecularAlbedo = args.SpecularAlbedo.GetNativeObject(NativeObjectType.D3D12Resource),
            Normals = args.Normals.GetNativeObject(NativeObjectType.D3D12Resource),
            Roughness = args.Roughness.GetNativeObject(NativeObjectType.D3D12Resource),
            Color = args.Color.GetNativeObject(NativeObjectType.D3D12Resource),
            Output = args.Output.GetNativeObject(NativeObjectType.D3D12Resource),
            Depth = args.Depth.GetNativeObject(NativeObjectType.D3D12Resource),
            MotionVectors = args.MotionVectors.GetNativeObject(NativeObjectType.D3D12Resource),
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            BiasCurrentColorMask = args.ReactiveMask?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            SpecularHitDistance = args.SpecularHitDistance.GetNativeObject(NativeObjectType.D3D12Resource),
            WorldToViewMatrix = &args.WorldToView,
            ViewToClipMatrix = &args.ViewToClip,
            PreExposure = args.PreExposure,
            ExposureScale = 1.0f,
            TransparencyLayer = args.TransparencyOverlay?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0
        };

        Ngx.D3D12.EvaluateDLSSDExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private static void EvaluateDirectX12(nint commandList, DLSSFeature feature, FrameGeneratorArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        NGXD3D12DLSSGEvalParams evalParams = new()
        {
            Backbuffer = (args.UI ?? args.Color).GetNativeObject(NativeObjectType.D3D12Resource),
            Depth = args.Depth.GetNativeObject(NativeObjectType.D3D12Resource),
            MVecs = args.MotionVectors.GetNativeObject(NativeObjectType.D3D12Resource),
            Hudless = args.UI is null ? 0 : args.Color.GetNativeObject(NativeObjectType.D3D12Resource),
            OutputInterpFrame = args.Output.GetNativeObject(NativeObjectType.D3D12Resource)
        };

        Ngx.D3D12.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams).Success();
    }
}
