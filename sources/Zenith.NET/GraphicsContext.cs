using Slangc.NET;

namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject, INativeObject
{
    protected GraphicsContext(GraphicsApi graphicsApi, bool useValidationLayer)
    {
        GraphicsApi = graphicsApi;

        Initialize(useValidationLayer,
                   out Capabilities capabilities,
                   out CommandQueue graphicsQueue,
                   out CommandQueue computeQueue,
                   out CommandQueue copyQueue,
                   out ValidationLayer? validationLayer);

        Capabilities = capabilities;
        GraphicsQueue = graphicsQueue;
        ComputeQueue = computeQueue;
        CopyQueue = copyQueue;
        ValidationLayer = validationLayer;

        Uploader = new(this);
        Downloader = new(this);
    }

    public GraphicsApi GraphicsApi { get; }

    public Capabilities Capabilities { get; }

    public CommandQueue GraphicsQueue { get; }

    public CommandQueue ComputeQueue { get; }

    public CommandQueue CopyQueue { get; }

    internal ValidationLayer? ValidationLayer { get; }

    internal Uploader Uploader { get; }

    internal Downloader Downloader { get; }

    public event EventHandler<ValidationMessageEventArgs>? ValidationMessage;

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        return CreateSwapChainImpl(desc);
    }

    public Heap CreateHeap(HeapDesc desc)
    {
        return CreateHeapImpl(desc);
    }

    public SizeAndAlignment GetSizeAndAlignment(BufferDesc desc)
    {
        return GetSizeAndAlignmentImpl(desc);
    }

    public SizeAndAlignment GetSizeAndAlignment(TextureDesc desc)
    {
        return GetSizeAndAlignmentImpl(desc);
    }

    public Buffer CreateBuffer(BufferDesc desc)
    {
        return CreateBufferImpl(desc);
    }

    public BufferView CreateBufferView(BufferViewDesc desc)
    {
        return CreateBufferViewImpl(desc);
    }

    public Texture CreateTexture(TextureDesc desc)
    {
        return CreateTextureImpl(desc);
    }

    public TextureView CreateTextureView(TextureViewDesc desc)
    {
        return CreateTextureViewImpl(desc);
    }

    public Sampler CreateSampler(SamplerDesc desc)
    {
        return CreateSamplerImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        return CreateShaderImpl(desc);
    }

    public GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        return CreateGraphicsPipelineImpl(desc);
    }

    public ComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
    {
        return CreateComputePipelineImpl(desc);
    }

    public MeshShadingPipeline CreateMeshShadingPipeline(MeshShadingPipelineDesc desc)
    {
        return CreateMeshShadingPipelineImpl(desc);
    }

    public QueryHeap CreateQueryHeap(QueryHeapDesc desc)
    {
        return CreateQueryHeapImpl(desc);
    }

    public Shader CreateShaderFromFile(string file, string entryPoint, ShaderStages stage, string[]? searchPaths)
    {
        return CreateShader(new()
        {
            Bytecode = SlangCompiler.Compile(SlangCompilerArguments(file, entryPoint, stage, searchPaths)),
            EntryPoint = entryPoint,
            Stage = stage
        });
    }

    public Shader CreateShaderFromSource(string source, string entryPoint, ShaderStages stage, string[]? searchPaths)
    {
        return CreateShader(new()
        {
            Bytecode = SlangCompiler.Compile(source, SlangCompilerArguments(null, entryPoint, stage, searchPaths)),
            EntryPoint = entryPoint,
            Stage = stage
        });
    }

    public abstract nint GetNativeObject(NativeObjectType type);

    protected override void Destroy()
    {
        GraphicsQueue.Dispose();
        ComputeQueue.Dispose();
        CopyQueue.Dispose();
        ValidationLayer?.Dispose();

        Uploader.Dispose();
        Downloader.Dispose();
    }

    protected abstract void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphicsQueue,
                                       out CommandQueue computeQueue,
                                       out CommandQueue copyQueue,
                                       out ValidationLayer? validationLayer);

    protected abstract SwapChain CreateSwapChainImpl(SwapChainDesc desc);

    protected abstract Heap CreateHeapImpl(HeapDesc desc);

    protected abstract SizeAndAlignment GetSizeAndAlignmentImpl(BufferDesc desc);

    protected abstract SizeAndAlignment GetSizeAndAlignmentImpl(TextureDesc desc);

    protected abstract Buffer CreateBufferImpl(BufferDesc desc);

    protected abstract BufferView CreateBufferViewImpl(BufferViewDesc desc);

    protected abstract Texture CreateTextureImpl(TextureDesc desc);

    protected abstract TextureView CreateTextureViewImpl(TextureViewDesc desc);

    protected abstract Sampler CreateSamplerImpl(SamplerDesc desc);

    protected abstract Shader CreateShaderImpl(ShaderDesc desc);

    protected abstract GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc);

    protected abstract ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc);

    protected abstract MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc);

    protected abstract QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc);

    internal void OnValidationMessage(ValidationMessageEventArgs args)
    {
        ValidationMessage?.Invoke(this, args);
    }

    private string[] SlangCompilerArguments(string? file, string entryPoint, ShaderStages stage, string[]? searchPaths)
    {
        List<string> arguments;

        if (file is null)
        {
            arguments =
            [
                "-entry", entryPoint,
                "-stage", stage.ToString().ToLowerInvariant(),
                "-matrix-layout-row-major"
            ];
        }
        else
        {
            arguments =
            [
                file,
                "-entry", entryPoint,
                "-stage", stage.ToString().ToLowerInvariant(),
                "-matrix-layout-row-major"
            ];
        }

        if (searchPaths is not null)
        {
            foreach (string path in searchPaths)
            {
                arguments.AddRange(["-I", path]);
            }
        }

        arguments.Add("-target");

        switch (GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                arguments.AddRange(["dxil", "-profile", "sm_6_6"]);
                break;

            case GraphicsApi.Metal:
                arguments.AddRange(["metallib", "-capability", "metallib_latest", "-Xmetal", "-std=metal4.0"]);
                break;

            case GraphicsApi.Vulkan:
                arguments.AddRange(["spirv", "-capability", "spirv_latest", "-capability", "spvDescriptorHeapEXT", "-fvk-use-entrypoint-name"]);
                break;
        }

        return [.. arguments];
    }
}
