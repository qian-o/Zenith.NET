using Zenith.NET;

namespace CornellBox.Models;

internal class PathTracingOutput : DisposableObject
{
    private readonly Texture[] textures = [];

    public PathTracingOutput(uint width, uint height)
    {
        TextureDesc desc = TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        };

        Color = App.Context.CreateTexture(desc);
        Depth = App.Context.CreateTexture(desc with { Format = PixelFormat.R32Float });
        MotionVectors = App.Context.CreateTexture(desc with { Format = PixelFormat.R32G32Float });
        DiffuseAlbedo = App.Context.CreateTexture(desc);
        SpecularAlbedo = App.Context.CreateTexture(desc);
        Normals = App.Context.CreateTexture(desc);
        Roughness = App.Context.CreateTexture(desc with { Format = PixelFormat.R16Float });
        SpecularHitDistance = App.Context.CreateTexture(desc with { Format = PixelFormat.R32Float });

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
