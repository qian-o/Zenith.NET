using Zenith.NET;

namespace CornellBox.Models;

internal class PathTracingOutput : DisposableObject
{
    private readonly Texture[] textures = [];

    public PathTracingOutput(uint width, uint height)
    {
        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        Depth = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R32Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        MotionVectors = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R32G32Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        DiffuseAlbedo = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        SpecularAlbedo = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        Normals = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        Roughness = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        SpecularHitDistance = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R32Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

        textures = [Color, Depth, MotionVectors, DiffuseAlbedo, SpecularAlbedo, Normals, Roughness, SpecularHitDistance];
    }

    public uint Width => Color.Desc.Width;

    public uint Height => Color.Desc.Height;

    public Texture Color { get; }

    public Texture Depth { get; }

    public Texture MotionVectors { get; }

    public Texture DiffuseAlbedo { get; }

    public Texture SpecularAlbedo { get; }

    public Texture Normals { get; }

    public Texture Roughness { get; }

    public Texture SpecularHitDistance { get; }

    public ReadOnlySpan<Texture> Textures => textures;

    protected override void Destroy()
    {
        SpecularHitDistance?.Dispose();
        Roughness?.Dispose();
        Normals?.Dispose();
        SpecularAlbedo?.Dispose();
        DiffuseAlbedo?.Dispose();
        MotionVectors?.Dispose();
        Depth?.Dispose();
        Color?.Dispose();
    }
}
