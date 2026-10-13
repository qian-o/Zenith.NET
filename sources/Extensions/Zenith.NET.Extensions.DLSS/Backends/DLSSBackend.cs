using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend : DisposableObject
{
    private readonly Dictionary<Texture, DLSSBinding> bindings = [];
    private readonly NGXStrings strings = new();
    private readonly ZenithMarshal.Scope scope = new();
    private readonly NGXParameter capabilityParameters;

    public DLSSBackend(GraphicsContext context, Guid project, string version, string dataDirectory)
    {
        Context = context;

        using Lock.Scope _ = Lock.EnterScope();

        Device = context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => context.GetNativeObject(NativeObjectType.D3D12Device),
            GraphicsApi.Vulkan => context.GetNativeObject(NativeObjectType.VulkanDevice),
            _ => default
        };

        byte* projectId = strings.AllocUtf8(project.ToString("D"));
        byte* engineVersion = strings.AllocUtf8(version);
        void* dataPath = strings.AllocWide(dataDirectory);
        void** paths = (void**)ZenithMarshal.Allocate<nint>(scope, 1);
        *paths = strings.AllocWide(Ngx.RuntimeDirectory);

        NGXFeatureCommonInfo featureInfo = new()
        {
            PathListInfo = new()
            {
                Path = paths,
                Length = 1
            }
        };

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                InitializeDirectX12(projectId, engineVersion, dataPath, &featureInfo);
                break;

            case GraphicsApi.Vulkan:
                InitializeVulkan(projectId, engineVersion, dataPath, &featureInfo);
                break;
        }

        fixed (NGXParameter* parameters = &capabilityParameters)
        {
            switch (context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    GetDirectX12CapabilityParameters(parameters);
                    break;

                case GraphicsApi.Vulkan:
                    GetVulkanCapabilityParameters(parameters);
                    break;
            }
        }

        Capabilities = new(GetI(capabilityParameters, Ngx.ParameterSuperSamplingAvailable) is not 0,
                           GetI(capabilityParameters, Ngx.ParameterSuperSamplingDenoisingAvailable) is not 0,
                           GetI(capabilityParameters, Ngx.ParameterFrameGenerationAvailable) is not 0);

        switch (context.GraphicsApi)
        {
            case GraphicsApi.Vulkan:
                InitializeVulkanResources();
                break;
        }
    }

    public static Lock Lock { get; } = new();

    public GraphicsContext Context { get; }

    public nint Device { get; }

    public DLSSCapabilities Capabilities { get; }

    public DLSSOptimalSettings GetOptimalSettings(NGXFeature feature, uint outputWidth, uint outputHeight, DLSSMode mode)
    {
        using Lock.Scope _ = Lock.EnterScope();

        uint inputWidth = 0;
        uint inputHeight = 0;
        uint maxInputWidth = 0;
        uint maxInputHeight = 0;
        uint minInputWidth = 0;
        uint minInputHeight = 0;
        float sharpness = 0.0f;

        NGXPerfQualityValue perfQualityValue = DLSSFormats.PerfQualityValue(mode);

        switch (feature)
        {
            case NGXFeature.SuperSampling:
                Ngx.DLSS.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, perfQualityValue, &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
                break;

            case NGXFeature.RayReconstruction:
                Ngx.DLSSD.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, perfQualityValue, &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
                break;
        }

        return new(inputWidth, inputHeight, minInputWidth, minInputHeight, maxInputWidth, maxInputHeight);
    }

    public NGXParameter AllocateParameters()
    {
        using Lock.Scope _ = Lock.EnterScope();

        NGXParameter parameters = default;

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                AllocateDirectX12Parameters(&parameters);
                break;

            case GraphicsApi.Vulkan:
                AllocateVulkanParameters(&parameters);
                break;
        }

        SetUI(parameters, Ngx.ParameterFreeMemOnReleaseFeature, 1);

        return parameters;
    }

    public void SetUI(NGXParameter parameters, ReadOnlySpan<byte> name, uint value)
    {
        using Lock.Scope _ = Lock.EnterScope();

        fixed (byte* pointer = name)
        {
            Ngx.Parameter.SetUI(parameters, pointer, value);
        }
    }

    public void DestroyParameters(NGXParameter parameters)
    {
        using Lock.Scope _ = Lock.EnterScope();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                DestroyDirectX12Parameters(parameters);
                break;

            case GraphicsApi.Vulkan:
                DestroyVulkanParameters(parameters);
                break;
        }
    }

    public void ReleaseFeature(NGXHandle handle)
    {
        using Lock.Scope _ = Lock.EnterScope();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                ReleaseDirectX12Feature(handle);
                break;

            case GraphicsApi.Vulkan:
                ReleaseVulkanFeature(handle);
                break;
        }
    }

    public nint CommandList(CommandBuffer commandBuffer)
    {
        return Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList),
            GraphicsApi.Vulkan => commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer),
            _ => default
        };
    }

    public void BindEmptyDescriptorSet(nint commandList)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.Vulkan:
                BindVulkanEmptyDescriptorSet(commandList);
                break;
        }
    }

    public DLSSBinding Binding(Texture texture)
    {
        using Lock.Scope _ = Lock.EnterScope();

        if (!bindings.TryGetValue(texture, out DLSSBinding binding))
        {
            NGXDimensions size = new()
            {
                Width = texture.Desc.Width,
                Height = texture.Desc.Height
            };

            bindings[texture] = binding = Context.GraphicsApi switch
            {
                GraphicsApi.DirectX12 => CreateDirectX12Binding(texture, size),
                GraphicsApi.Vulkan => CreateVulkanBinding(texture, size),
                _ => default
            };

            texture.Disposing += (sender, _) =>
            {
                using Lock.Scope scope = Lock.EnterScope();

                Texture disposedTexture = (Texture)sender!;

                if (bindings.Remove(disposedTexture, out DLSSBinding disposedBinding))
                {
                    switch (Context.GraphicsApi)
                    {
                        case GraphicsApi.Vulkan:
                            DestroyVulkanBinding(disposedBinding);
                            break;
                    }
                }
            };
        }

        return binding;
    }

    protected override void Destroy()
    {
        using Lock.Scope _ = Lock.EnterScope();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.Vulkan:
                DestroyVulkanResources();
                break;
        }

        bindings.Clear();

        DestroyParameters(capabilityParameters);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                ShutdownDirectX12();
                break;

            case GraphicsApi.Vulkan:
                ShutdownVulkan();
                break;
        }

        scope.Dispose();
        strings.Dispose();
    }

    private static int GetI(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            int value = 0;
            Ngx.Parameter.GetI(parameters, pointer, &value).Success();

            return value;
        }
    }
}
