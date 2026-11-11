using Zenith.NET;

namespace CornellBox.Models;

internal class PathTracingOutput : DisposableObject
{
    public const PixelFormat ColorFormat = PixelFormat.R16G16B16A16Float;
    public const PixelFormat DepthFormat = PixelFormat.R32Float;
    public const PixelFormat MotionVectorsFormat = PixelFormat.R32G32Float;
    public const PixelFormat DiffuseAlbedoFormat = PixelFormat.R16G16B16A16Float;
    public const PixelFormat SpecularAlbedoFormat = PixelFormat.R16G16B16A16Float;
    public const PixelFormat NormalsFormat = PixelFormat.R16G16B16A16Float;
    public const PixelFormat RoughnessFormat = PixelFormat.R16Float;
    public const PixelFormat SpecularHitDistanceFormat = PixelFormat.R32Float;
    public const PixelFormat ReactiveMaskFormat = PixelFormat.R16Float;

    private readonly Texture[] textures;

    public PathTracingOutput(uint width, uint height)
    {
        Color = CreateTexture(ColorFormat, width, height);
        Depth = CreateTexture(DepthFormat, width, height, TextureUsages.TransferSrc);
        MotionVectors = CreateTexture(MotionVectorsFormat, width, height, TextureUsages.TransferSrc);
        DiffuseAlbedo = CreateTexture(DiffuseAlbedoFormat, width, height);
        SpecularAlbedo = CreateTexture(SpecularAlbedoFormat, width, height);
        Normals = CreateTexture(NormalsFormat, width, height);
        Roughness = CreateTexture(RoughnessFormat, width, height);
        SpecularHitDistance = CreateTexture(SpecularHitDistanceFormat, width, height);
        ReactiveMask = CreateTexture(ReactiveMaskFormat, width, height);

        textures = [Color, Depth, MotionVectors, DiffuseAlbedo, SpecularAlbedo, Normals, Roughness, SpecularHitDistance, ReactiveMask];
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

    public Texture ReactiveMask { get; }

    public ReadOnlySpan<Texture> Textures => textures;

    protected override void Destroy()
    {
        for (int i = 0; i < textures.Length; i++)
        {
            textures[i].Dispose();
        }
    }

    private static Texture CreateTexture(PixelFormat format, uint width, uint height, TextureUsages additionalUsages = TextureUsages.None)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | additionalUsages
        });
    }
}
