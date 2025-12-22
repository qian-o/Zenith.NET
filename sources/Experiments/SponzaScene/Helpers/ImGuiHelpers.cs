using Hexa.NET.ImGui;

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
}
