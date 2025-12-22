using Hexa.NET.ImGui;

namespace SponzaScene.Helpers;

internal static class ImGuiHelpers
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoDecoration
                                                  | ImGuiWindowFlags.AlwaysAutoResize
                                                  | ImGuiWindowFlags.NoSavedSettings
                                                  | ImGuiWindowFlags.NoFocusOnAppearing
                                                  | ImGuiWindowFlags.NoNav
                                                  | ImGuiWindowFlags.NoMove;

    public static void LeftTopOverlay(string name, Action action)
    {
        ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.Always, new(0, 0));
        ImGui.SetNextWindowBgAlpha(0.35f);

        Overlay(name, action);
    }

    public static void RightTopOverlay(string name, Action action)
    {
        ImGui.SetNextWindowPos(new(ImGui.GetIO().DisplaySize.X - 10, 10), ImGuiCond.Always, new(1, 0));
        ImGui.SetNextWindowBgAlpha(0.35f);

        Overlay(name, action);
    }

    public static void LeftBottomOverlay(string name, Action action)
    {
        ImGui.SetNextWindowPos(new(10, ImGui.GetIO().DisplaySize.Y - 10), ImGuiCond.Always, new(0, 1));
        ImGui.SetNextWindowBgAlpha(0.35f);

        Overlay(name, action);
    }

    public static void RightBottomOverlay(string name, Action action)
    {
        ImGui.SetNextWindowPos(new(ImGui.GetIO().DisplaySize.X - 10, ImGui.GetIO().DisplaySize.Y - 10), ImGuiCond.Always, new(1, 1));
        ImGui.SetNextWindowBgAlpha(0.35f);

        Overlay(name, action);
    }

    private static void Overlay(string name, Action action)
    {
        if (ImGui.Begin(name, OverlayFlags))
        {
            action();

            ImGui.End();
        }
    }
}
