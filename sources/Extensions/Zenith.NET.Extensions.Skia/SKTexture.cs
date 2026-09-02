using System.Numerics;
using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

public class SKTexture : DisposableObject
{
    private readonly Texture texture;
    private readonly SKSurface surface;

    internal SKTexture(SKRenderer renderer, SKTextureDesc desc)
    {
        Renderer = renderer;

        texture = renderer.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = desc.Format,
            Width = desc.Width,
            Height = desc.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment | TextureUsages.TransferSrc | TextureUsages.TransferDst
        });

        CommandBuffer commandBuffer = renderer.Context.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        commandBuffer.BeginRenderPass([ColorAttachment.Clear(texture, Vector4.Zero)], null);
        commandBuffer.EndRenderPass();

        if ((RequiredLayout = desc.IsMultisamplingEnabled ? TextureLayout.ResolveDst : TextureLayout.ColorAttachment) is not TextureLayout.ColorAttachment)
        {
            commandBuffer.Transition(texture, default, TextureLayout.ColorAttachment, RequiredLayout);
        }

        commandBuffer.Submit().Wait();

        using GRBackendTexture backendTexture = renderer.CreateBackendTexture(texture, desc.IsMultisamplingEnabled);

        surface = SKSurface.Create(renderer.GRContext, backendTexture, GRSurfaceOrigin.TopLeft, desc.IsMultisamplingEnabled ? 4 : 1, SKFormats.Skia(desc.Format));

        Desc = desc;
    }

    public SKTextureDesc Desc { get; }

    public TextureLayout RequiredLayout { get; }

    internal SKRenderer Renderer { get; }

    public void Render(Action<SKCanvas> render)
    {
        Renderer.Render(surface, render);
    }

    protected override void Destroy()
    {
        surface.Dispose();
        texture.Dispose();

        Extensions.ReleaseRenderer(Renderer);
    }

    public static implicit operator Texture(SKTexture texture)
    {
        return texture.texture;
    }
}
