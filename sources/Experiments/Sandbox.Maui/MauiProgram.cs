using Zenith.NET.Views.Maui;

namespace Sandbox.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        return MauiApp.CreateBuilder()
                      .UseMauiApp<App>()
                      .UseZenithView()
                      .ConfigureFonts(Configure)
                      .Build();
    }

    private static void Configure(IFontCollection fonts)
    {
        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
    }
}
