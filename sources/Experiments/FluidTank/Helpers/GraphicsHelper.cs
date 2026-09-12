using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Helpers;

internal static unsafe class GraphicsHelper
{
    public static Buffer CreateBuffer(GraphicsContext context, uint count, uint strideInBytes, BufferUsages usages)
    {
        return context.CreateBuffer(new()
        {
            SizeInBytes = count * strideInBytes,
            StrideInBytes = strideInBytes,
            Usages = usages,
            Residency = MemoryResidency.GpuOnly
        });
    }

    public static Buffer LoadBuffer<T>(GraphicsContext context, CommandBuffer commandBuffer, T[] data, BufferUsages usages) where T : unmanaged
    {
        Buffer buffer = CreateBuffer(context, (uint)data.Length, (uint)sizeof(T), usages | BufferUsages.TransferDst);

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

    public static Buffer CreateConstantBuffer(GraphicsContext context, uint sizeInBytes)
    {
        return context.CreateBuffer(new()
        {
            SizeInBytes = sizeInBytes,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
    }

    public static Buffer CreateConstantBuffer<T>(GraphicsContext context) where T : unmanaged
    {
        return CreateConstantBuffer(context, (uint)sizeof(T));
    }

    public static Texture CreateTexture(GraphicsContext context, PixelFormat format, uint width, uint height, TextureUsages usages)
    {
        return context.CreateTexture(new()
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

    public static Shader LoadShader(GraphicsContext context, string file, string entryPoint)
    {
        return context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, Path.Combine([AppContext.BaseDirectory, "Assets", "Shaders", file]), entryPoint));
    }

    public static GraphicsPipeline CreateGraphicsPipeline(GraphicsContext context, string file,
                                                          string vertexEntryPoint,
                                                          string fragmentEntryPoint,
                                                          InputLayout[] inputLayouts,
                                                          AttachmentFormats attachmentFormats,
                                                          RasterizerState rasterizer,
                                                          DepthStencilState depthStencil,
                                                          BlendState blend,
                                                          PrimitiveTopology primitiveTopology = PrimitiveTopology.TriangleList)
    {
        using Shader vertexShader = LoadShader(context, file, vertexEntryPoint);

        return CreateGraphicsPipeline(context, vertexShader, file, fragmentEntryPoint, inputLayouts, attachmentFormats, rasterizer, depthStencil, blend, primitiveTopology);
    }

    public static GraphicsPipeline CreateGraphicsPipeline(GraphicsContext context, Shader vertexShader,
                                                          string file,
                                                          string fragmentEntryPoint,
                                                          InputLayout[] inputLayouts,
                                                          AttachmentFormats attachmentFormats,
                                                          RasterizerState rasterizer,
                                                          DepthStencilState depthStencil,
                                                          BlendState blend,
                                                          PrimitiveTopology primitiveTopology = PrimitiveTopology.TriangleList)
    {
        using Shader fragmentShader = LoadShader(context, file, fragmentEntryPoint);

        return context.CreateGraphicsPipeline(new()
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

    public static ComputePipeline CreateComputePipeline(GraphicsContext context, string file, string entryPoint)
    {
        using Shader shader = LoadShader(context, file, entryPoint);

        return context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public static void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline, uint width, uint height)
    {
        ThreadGroupSize groupSize = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.Dispatch((width + groupSize.X - 1) / groupSize.X, (height + groupSize.Y - 1) / groupSize.Y, 1);
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
