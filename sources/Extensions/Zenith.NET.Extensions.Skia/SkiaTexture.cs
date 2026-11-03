using System.Numerics;
using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

public class SkiaTexture : DisposableObject
{
    private readonly SkiaBridge bridge;
    private readonly Texture texture;
    private readonly SKSurface surface;

    internal SkiaTexture(SkiaBridge bridge, SkiaTextureDesc desc)
    {
        this.bridge = bridge;

        texture = bridge.Context.CreateTexture(new()
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

        CommandBuffer commandBuffer = bridge.Context.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        commandBuffer.BeginRenderPass([ColorAttachment.Clear(texture, Vector4.Zero)], null);
        commandBuffer.EndRenderPass();

        if ((RequiredLayout = desc.IsMultisamplingEnabled ? TextureLayout.ResolveDst : TextureLayout.ColorAttachment) is not TextureLayout.ColorAttachment)
        {
            commandBuffer.Transition(texture, default, TextureLayout.ColorAttachment, RequiredLayout);
        }

        commandBuffer.Submit().Wait();

        using GRBackendTexture backendTexture = bridge.CreateBackendTexture(texture, desc.IsMultisamplingEnabled);

        surface = SKSurface.Create(bridge.GRContext, backendTexture, GRSurfaceOrigin.TopLeft, desc.IsMultisamplingEnabled ? 4 : 1, SkiaFormats.Skia(desc.Format));

        Desc = desc;
    }

    public SkiaTextureDesc Desc { get; }

    public TextureLayout RequiredLayout { get; }

    public void Render(Action<SKCanvas> render)
    {
        bridge.Render(surface, render);
    }

    protected override void Destroy()
    {
        surface.Dispose();
        texture.Dispose();
    }

    public static implicit operator Texture(SkiaTexture texture)
    {
        return texture.texture;
    }
}
