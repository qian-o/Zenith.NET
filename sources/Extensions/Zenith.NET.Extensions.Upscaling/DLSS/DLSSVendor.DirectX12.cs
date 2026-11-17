using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor
{
    private void InitializeDirectX12(string projectId, string engineVersion, string dataPath, NGXFeatureCommonInfo featureInfo)
    {
        Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, device, &featureInfo, NGXVersion.Api).Success();
    }

    private void DestroyDirectX12()
    {
        Ngx.D3D12.Shutdown1(device).Success();
    }

    private static NGXHandle CreateFeatureDirectX12(nint commandList, NGXParameter parameters, NGXDLSSCreateParams createParams)
    {
        NGXHandle handle = default;
        Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, &handle, parameters, &createParams).Success();

        return handle;
    }

    private static NGXHandle CreateFeatureDirectX12(nint commandList, NGXParameter parameters, NGXDLSSDCreateParams createParams)
    {
        NGXHandle handle = default;
        Ngx.D3D12.CreateDLSSDExt(commandList, 1, 1, &handle, parameters, &createParams).Success();

        return handle;
    }

    private static NGXHandle CreateFeatureDirectX12(nint commandList, NGXParameter parameters, NGXDLSSGCreateParams createParams)
    {
        NGXHandle handle = default;
        Ngx.D3D12.CreateDLSSG(commandList, 1, 1, &handle, parameters, &createParams).Success();

        return handle;
    }

    private static void EvaluateFeatureDirectX12(nint commandList, NGXParameter parameters, NGXHandle handle, SuperResolutionArgs args, Texture output)
    {
        NGXD3D12DLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = args.Color.DirectX12(),
                Output = output.DirectX12()
            },
            Depth = args.Depth.DirectX12(),
            MotionVectors = args.MotionVectors.DirectX12(),
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
            ExposureTexture = args.Exposure?.DirectX12() ?? 0,
            BiasCurrentColorMask = args.ReactiveMask?.DirectX12() ?? 0,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f
        };

        Ngx.D3D12.EvaluateDLSSExt(commandList, handle, parameters, &evalParams).Success();
    }

    private static void EvaluateFeatureDirectX12(nint commandList, NGXParameter parameters, NGXHandle handle, RayReconstructionArgs args, Texture output)
    {
        NGXD3D12DLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = args.DiffuseAlbedo.DirectX12(),
            SpecularAlbedo = args.SpecularAlbedo.DirectX12(),
            Normals = args.Normals.DirectX12(),
            Roughness = args.Roughness.DirectX12(),
            Color = args.Color.DirectX12(),
            Output = output.DirectX12(),
            Depth = args.Depth.DirectX12(),
            MotionVectors = args.MotionVectors.DirectX12(),
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
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

        Ngx.D3D12.EvaluateDLSSDExt(commandList, handle, parameters, &evalParams).Success();
    }

    private static void EvaluateFeatureDirectX12(nint commandList, NGXParameter parameters, NGXHandle handle, FrameGenerationArgs args, Texture output, NGXDLSSGOptEvalParams optEvalParams)
    {
        NGXD3D12DLSSGEvalParams evalParams = new()
        {
            Backbuffer = args.Color.DirectX12(),
            Depth = args.Depth.DirectX12(),
            MVecs = args.MotionVectors.DirectX12(),
            Hudless = args.Hudless?.DirectX12() ?? 0,
            OutputInterpFrame = output.DirectX12()
        };

        Ngx.D3D12.EvaluateDLSSG(commandList, handle, parameters, &evalParams, &optEvalParams).Success();
    }
}
