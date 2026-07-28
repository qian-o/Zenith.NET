using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Helpers;

internal static unsafe class GraphicsHelper
{
    public static string ShaderPath(params string[] paths)
    {
        return Path.Combine([AppContext.BaseDirectory, "Assets", "Shaders", .. paths]);
    }

    public static Buffer CreateBuffer(uint count, uint strideInBytes, BufferUsages usages)
    {
        return App.Context.CreateBuffer(new()
        {
            SizeInBytes = count * strideInBytes,
            StrideInBytes = strideInBytes,
            Usages = usages,
            Residency = MemoryResidency.GpuOnly
        });
    }

    public static Buffer LoadBuffer<T>(CommandBuffer commandBuffer, T[] data, BufferUsages usages) where T : unmanaged
    {
        Buffer buffer = CreateBuffer((uint)data.Length, (uint)sizeof(T), usages | BufferUsages.TransferDst);

        fixed (T* pointer = data)
        {
            commandBuffer.Upload(buffer, 0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(T) * data.Length)
            });
        }

        return buffer;
    }

    public static Buffer CreateConstantBuffer(uint sizeInBytes)
    {
        return App.Context.CreateBuffer(new()
        {
            SizeInBytes = sizeInBytes,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
    }

    public static Buffer CreateConstantBuffer<T>() where T : unmanaged
    {
        return CreateConstantBuffer((uint)sizeof(T));
    }

    public static Texture CreateTexture(PixelFormat format, uint width, uint height, TextureUsages usages)
    {
        return App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = format,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = usages
        });
    }

    public static Shader LoadShader(string file, string entryPoint)
    {
        return App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath(file), entryPoint));
    }

    public static GraphicsPipeline CreateGraphicsPipeline(string file,
                                                          string vertexEntryPoint,
                                                          string fragmentEntryPoint,
                                                          InputLayout[] inputLayouts,
                                                          AttachmentFormats attachmentFormats,
                                                          RasterizerState rasterizer,
                                                          DepthStencilState depthStencil,
                                                          BlendState blend,
                                                          PrimitiveTopology primitiveTopology = PrimitiveTopology.TriangleList)
    {
        using Shader vertexShader = LoadShader(file, vertexEntryPoint);

        return CreateGraphicsPipeline(vertexShader,
                                      file,
                                      fragmentEntryPoint,
                                      inputLayouts,
                                      attachmentFormats,
                                      rasterizer,
                                      depthStencil,
                                      blend,
                                      primitiveTopology);
    }

    public static GraphicsPipeline CreateGraphicsPipeline(Shader vertexShader,
                                                          string file,
                                                          string fragmentEntryPoint,
                                                          InputLayout[] inputLayouts,
                                                          AttachmentFormats attachmentFormats,
                                                          RasterizerState rasterizer,
                                                          DepthStencilState depthStencil,
                                                          BlendState blend,
                                                          PrimitiveTopology primitiveTopology = PrimitiveTopology.TriangleList)
    {
        using Shader fragmentShader = LoadShader(file, fragmentEntryPoint);

        return App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = inputLayouts,
            PrimitiveTopology = primitiveTopology,
            AttachmentFormats = attachmentFormats,
            RenderState = new()
            {
                Rasterizer = rasterizer,
                DepthStencil = depthStencil,
                Blend = blend
            }
        });
    }

    public static ComputePipeline CreateComputePipeline(string file, string entryPoint)
    {
        using Shader shader = LoadShader(file, entryPoint);

        return App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public static void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline, uint width, uint height)
    {
        ThreadGroupSize groupSize = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.Dispatch((width + groupSize.X - 1) / groupSize.X,
                               (height + groupSize.Y - 1) / groupSize.Y,
                               1);
    }

    public static void Upload(Buffer buffer, uint offsetInBytes, void* data, uint sizeInBytes)
    {
        buffer.Upload(offsetInBytes, new()
        {
            Pointer = (nint)data,
            SizeInBytes = sizeInBytes
        });
    }
}
