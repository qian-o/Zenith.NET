using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;

namespace SponzaScene.Helpers;

internal static class ImGuiHelpers
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoDecoration
                                                  | ImGuiWindowFlags.AlwaysAutoResize
                                                  | ImGuiWindowFlags.NoSavedSettings
                                                  | ImGuiWindowFlags.NoFocusOnAppearing
                                                  | ImGuiWindowFlags.NoInputs
                                                  | ImGuiWindowFlags.NoMove;

    public static void Overlay(string name, Action action)
    {
        ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.Always, new(0, 0));
        ImGui.SetNextWindowBgAlpha(0.35f);

        if (ImGui.Begin(name, OverlayFlags))
        {
            action();

            ImGui.End();
        }
    }

    public static void Image(Texture texture, Vector2? maxSize = null)
    {
        float aspectRatio = (float)texture.Desc.Width / texture.Desc.Height;

        Vector2 size = maxSize ?? ImGui.GetContentRegionAvail();

        if (size.X / size.Y > aspectRatio)
        {
            size.X = size.Y * aspectRatio;
        }
        else
        {
            size.Y = size.X / aspectRatio;
        }

        Image(App.Binding(texture), size);
    }

    public static void Image(TextureView textureView, Vector2? maxSize = null)
    {
        ZenithHelper.MipDimensions(textureView.Desc.Texture.Desc.Width,
                                   textureView.Desc.Texture.Desc.Height,
                                   1,
                                   textureView.Desc.FirstMipLevel,
                                   out uint mipWidth,
                                   out uint mipHeight,
                                   out _);

        float aspectRatio = (float)mipWidth / mipHeight;

        Vector2 size = maxSize ?? ImGui.GetContentRegionAvail();

        if (size.X / size.Y > aspectRatio)
        {
            size.X = size.Y * aspectRatio;
        }
        else
        {
            size.Y = size.X / aspectRatio;
        }

        Image(App.Binding(textureView), size);
    }

    private static void Image(ImTextureRef imTextureRef, Vector2 size)
    {
        ImGui.Image(imTextureRef, size);

        if (ImGui.IsItemHovered() && ImGui.BeginTooltip())
        {
            Vector2 displaySize = ImGui.GetIO().DisplaySize / 4.0f;

            float aspectRatio = size.X / size.Y;

            float popupWidth = displaySize.X;
            float popupHeight = popupWidth / aspectRatio;

            if (popupHeight > displaySize.Y)
            {
                popupHeight = displaySize.Y;
                popupWidth = popupHeight * aspectRatio;
            }

            ImGui.Image(imTextureRef, new(popupWidth, popupHeight));

            ImGui.EndTooltip();
        }
    }
}
