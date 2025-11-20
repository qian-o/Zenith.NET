using Hexa.NET.ImGui;
using Zenith.NET;

namespace SponzaScene;

internal class MainView : DisposableObject, IView
{
    public void Update(double delta)
    {
    }

    public void Render(double delta)
    {
        ImGui.ShowDemoWindow();
    }

    protected override void Destroy()
    {
    }
}
