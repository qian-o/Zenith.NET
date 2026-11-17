using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor : Vendor
{
    private nint device;

    public DLSSVendor(GraphicsContext context) : base(context)
    {
        string projectId = typeof(GraphicsContext).GUID.ToString();
        string engineVersion = typeof(GraphicsContext).Assembly.GetName().Version!.ToString();
        string dataPath = Path.Combine(Path.GetTempPath(), "Zenith.NET", "NGX");

        Directory.CreateDirectory(dataPath);

        using NGXStrings strings = new();
        void* runtimeDirectory = strings.AllocWide(Ngx.RuntimeDirectory);

        NGXFeatureCommonInfo commonInfo = new()
        {
            PathListInfo = new()
            {
                Path = &runtimeDirectory,
                Length = 1
            }
        };

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                InitializeDirectX12(projectId, engineVersion, dataPath, &commonInfo);
                break;

            case GraphicsApi.Vulkan:
                InitializeVulkan(projectId, engineVersion, dataPath, &commonInfo);
                break;
        }

        NGXParameter parameters = default;
        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.GetCapabilityParameters(&parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.GetCapabilityParameters(&parameters).Success();
                break;
        }

        Capabilities = new(DLSSHelper.GetBool(parameters, Ngx.ParameterSuperSamplingAvailable), DLSSHelper.GetBool(parameters, Ngx.ParameterSuperSamplingDenoisingAvailable), DLSSHelper.GetBool(parameters, Ngx.ParameterFrameGenerationAvailable));

        DestroyParameters(parameters);
    }

    public override UpscalingCapabilities Capabilities { get; }

    public override SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return new DLSSSuperResolution(this, desc);
    }

    public override RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return new DLSSRayReconstruction(this, desc);
    }

    public override FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return new DLSSFrameGeneration(this, desc);
    }

    public NGXParameter CreateParameters()
    {
        NGXParameter parameters = default;
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.AllocateParameters(&parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.AllocateParameters(&parameters).Success();
                break;
        }

        return parameters;
    }

    public void DestroyParameters(NGXParameter parameters)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.DestroyParameters(parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.DestroyParameters(parameters).Success();
                break;
        }
    }

    public NGXHandle CreateFeature(NGXParameter parameters, SuperResolutionDesc desc, SuperResolutionArgs args)
    {
        DLSSHelper.ResetParameters(parameters);

        NGXDLSSCreateParams createParams = new()
        {
            Feature = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight,
                TargetWidth = desc.OutputWidth,
                TargetHeight = desc.OutputHeight,
                PerfQualityValue = DLSSHelper.PerfQuality(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight)
            },
            FeatureCreateFlags = DLSSHelper.FeatureFlags(args.Color.Desc.Format, args.Exposure, args.Projection)
        };

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        NGXHandle handle = Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => CreateFeatureDirectX12(commandBuffer.DirectX12(), parameters, &createParams),
            GraphicsApi.Vulkan => CreateFeatureVulkan(commandBuffer.Vulkan(), parameters, &createParams),
            _ => default
        };

        commandBuffer.Submit().Wait();

        return handle;
    }

    public NGXHandle CreateFeature(NGXParameter parameters, RayReconstructionDesc desc, RayReconstructionArgs args)
    {
        DLSSHelper.ResetParameters(parameters);

        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = desc.RenderWidth,
            Height = desc.RenderHeight,
            TargetWidth = desc.OutputWidth,
            TargetHeight = desc.OutputHeight,
            PerfQualityValue = DLSSHelper.PerfQuality(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight),
            FeatureCreateFlags = DLSSHelper.FeatureFlags(args.Color.Desc.Format, args.Exposure, args.Projection)
        };

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        NGXHandle handle = Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => CreateFeatureDirectX12(commandBuffer.DirectX12(), parameters, &createParams),
            GraphicsApi.Vulkan => CreateFeatureVulkan(commandBuffer.Vulkan(), parameters, &createParams),
            _ => default
        };

        commandBuffer.Submit().Wait();

        return handle;
    }

    public NGXHandle CreateFeature(NGXParameter parameters, FrameGenerationDesc desc)
    {
        DLSSHelper.ResetParameters(parameters);

        NGXDLSSGCreateParams createParams = new()
        {
            Width = desc.OutputWidth,
            Height = desc.OutputHeight,
            NativeBackbufferFormat = Context.GraphicsApi switch
            {
                GraphicsApi.DirectX12 => DLSSFormats.DirectX12(desc.Format),
                GraphicsApi.Vulkan => (uint)DLSSFormats.Vulkan(desc.Format),
                _ => default
            },
            RenderWidth = desc.RenderWidth,
            RenderHeight = desc.RenderHeight
        };

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        NGXHandle handle = Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => CreateFeatureDirectX12(commandBuffer.DirectX12(), parameters, &createParams),
            GraphicsApi.Vulkan => CreateFeatureVulkan(commandBuffer.Vulkan(), parameters, &createParams),
            _ => default
        };

        commandBuffer.Submit().Wait();

        return handle;
    }

    public void DestroyFeature(NGXHandle handle)
    {
        if (handle.IsNull)
        {
            return;
        }

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.ReleaseFeature(handle).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.ReleaseFeature(handle).Success();
                break;
        }
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, NGXParameter parameters, NGXHandle handle, SuperResolutionArgs args, Texture output)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateFeatureDirectX12(commandBuffer.DirectX12(), parameters, handle, args, output);
                break;

            case GraphicsApi.Vulkan:
                EvaluateFeatureVulkan(commandBuffer.Vulkan(), parameters, handle, args, output);
                break;
        }
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, NGXParameter parameters, NGXHandle handle, RayReconstructionArgs args, Texture output)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateFeatureDirectX12(commandBuffer.DirectX12(), parameters, handle, args, output);
                break;

            case GraphicsApi.Vulkan:
                EvaluateFeatureVulkan(commandBuffer.Vulkan(), parameters, handle, args, output);
                break;
        }
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, NGXParameter parameters, NGXHandle handle, Matrix4x4 previousViewProjection, FrameGenerationArgs args, Texture output)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateFeatureDirectX12(commandBuffer.DirectX12(), parameters, handle, previousViewProjection, args, output);
                break;

            case GraphicsApi.Vulkan:
                EvaluateFeatureVulkan(commandBuffer.Vulkan(), parameters, handle, previousViewProjection, args, output);
                break;
        }
    }

    protected override void Destroy()
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                ShutdownDirectX12();
                break;

            case GraphicsApi.Vulkan:
                ShutdownVulkan();
                break;
        }
    }
}
