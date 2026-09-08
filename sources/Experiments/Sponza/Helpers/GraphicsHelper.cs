using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Helpers;

internal static unsafe class GraphicsHelper
{
    public static Buffer CreateConstantBuffer<T>(GraphicsContext context) where T : unmanaged
    {
        return context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(T),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
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
        string directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders");

        return context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, Path.Combine(directory, file), entryPoint, [directory]));
    }

    public static ComputePipeline CreateComputePipeline(GraphicsContext context, string file, string entryPoint)
    {
        using Shader shader = LoadShader(context, file, entryPoint);

        return context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public static void Upload<T>(Buffer buffer, T value) where T : unmanaged
    {
        buffer.Upload(0, new()
        {
            Pointer = (nint)(&value),
            SizeInBytes = (uint)sizeof(T)
        });
    }
}
