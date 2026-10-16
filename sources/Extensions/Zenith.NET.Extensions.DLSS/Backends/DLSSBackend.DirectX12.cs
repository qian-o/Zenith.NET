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

    private static void EvaluateDirectX12(nint commandList, DLSSSuperResolution feature, DLSSSuperResolutionArgs args)
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
            RenderSubrectDimensions = args.Color.Size,
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            ExposureTexture = args.Exposure?.DirectX12 ?? 0,
            PreExposure = args.PreExposure
        };

        Ngx.D3D12.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private static void EvaluateDirectX12(nint commandList, DLSSRayReconstruction feature, DLSSRayReconstructionArgs args)
    {
        NGXD3D12DLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = args.DiffuseAlbedo.DirectX12,
            SpecularAlbedo = args.SpecularAlbedo.DirectX12,
            Normals = args.Normals.DirectX12,
            Roughness = args.Roughness.DirectX12,
            Color = args.Color.DirectX12,
            Output = args.Output.DirectX12,
            Depth = args.Depth.DirectX12,
            MotionVectors = args.MotionVectors.DirectX12,
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            RenderSubrectDimensions = args.Color.Size,
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            SpecularHitDistance = args.SpecularHitDistance.DirectX12,
            WorldToViewMatrix = &args.WorldToView,
            ViewToClipMatrix = &args.ViewToClip,
            PreExposure = args.PreExposure,
            TransparencyLayer = args.TransparencyLayer?.DirectX12 ?? 0
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
