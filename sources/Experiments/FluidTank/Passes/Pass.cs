using FluidTank.Models;
using Zenith.NET;

namespace FluidTank.Passes;

internal abstract class Pass : DisposableObject
{
    protected Pass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight)
    {
        RenderWidth = renderWidth;
        RenderHeight = renderHeight;
        DisplayWidth = displayWidth;
        DisplayHeight = displayHeight;

        Initialize();
    }

    public uint RenderWidth { get; private set; }

    public uint RenderHeight { get; private set; }

    public uint DisplayWidth { get; private set; }

    public uint DisplayHeight { get; private set; }

    public void Record(CommandBuffer commandBuffer, in PassArgs args)
    {
        if (RenderWidth is 0 || RenderHeight is 0 || DisplayWidth is 0 || DisplayHeight is 0)
        {
            return;
        }

        RecordImpl(commandBuffer, args);
    }

    public void Resize(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight)
    {
        RenderWidth = renderWidth;
        RenderHeight = renderHeight;
        DisplayWidth = displayWidth;
        DisplayHeight = displayHeight;

        ResizeImpl();
    }

    protected abstract void Initialize();

    protected abstract void RecordImpl(CommandBuffer commandBuffer, in PassArgs args);

    protected abstract void ResizeImpl();

    protected static string ShaderPath(string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", file);
    }

    protected static Texture CreateTexture(uint width, uint height, PixelFormat format, TextureUsages usages = TextureUsages.Sampled | TextureUsages.Storage)
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
}
