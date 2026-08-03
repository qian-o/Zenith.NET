using SkiaSharp;

namespace SkiaGallery;

internal class GalleryResources : IDisposable
{
    public GalleryResources()
    {
        string family = OperatingSystem.IsMacOS() ? "SF Pro Display" : OperatingSystem.IsWindows() ? "Segoe UI" : "Noto Sans";

        RegularTypeface = SKTypeface.FromFamilyName(family, SKFontStyle.Normal);
        MediumTypeface = SKTypeface.FromFamilyName(family, new(SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright));

        CaptionFont = CreateFont(RegularTypeface, 12.0f);
        NavigationFont = CreateFont(MediumTypeface, 15.0f);
        BodyFont = CreateFont(RegularTypeface, 16.0f);
        SectionFont = CreateFont(MediumTypeface, 22.0f);
        TitleFont = CreateFont(MediumTypeface, 34.0f);
    }

    public SKTypeface RegularTypeface { get; }

    public SKTypeface MediumTypeface { get; }

    public SKFont CaptionFont { get; }

    public SKFont NavigationFont { get; }

    public SKFont BodyFont { get; }

    public SKFont SectionFont { get; }

    public SKFont TitleFont { get; }

    public void Dispose()
    {
        TitleFont.Dispose();
        SectionFont.Dispose();
        BodyFont.Dispose();
        NavigationFont.Dispose();
        CaptionFont.Dispose();
        MediumTypeface.Dispose();
        RegularTypeface.Dispose();
    }

    public static SKTextBlob CreateText(string text, SKFont font)
    {
        return SKTextBlob.Create(text, font, default)!;
    }

    private static SKFont CreateFont(SKTypeface typeface, float size)
    {
        return new(typeface, size)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Slight,
            Subpixel = true
        };
    }
}