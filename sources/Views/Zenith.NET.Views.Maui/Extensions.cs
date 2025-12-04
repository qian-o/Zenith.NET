using Zenith.NET.Views.Maui;

namespace Zenith.NET.Views.Maui;

public static class Extensions
{
    extension(MauiAppBuilder builder)
    {
        public MauiAppBuilder UseZenithView()
        {
            return builder.ConfigureMauiHandlers(static handlers => handlers.AddHandler<ZenithView, ZenithViewHandler>());
        }
    }
}
