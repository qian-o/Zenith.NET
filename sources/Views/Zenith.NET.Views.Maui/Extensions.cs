using Zenith.NET.Views.Maui;

namespace Zenith.NET.Views.Maui;

public static class Extensions
{
    extension(MauiAppBuilder builder)
    {
        public MauiAppBuilder UseZenithView()
        {
            return builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<ZenithView, ZenithViewHandler>());
        }
    }
}
