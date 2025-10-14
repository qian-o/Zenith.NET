namespace Zenith.NET.Views;

public static class Extensions
{
    public static MauiAppBuilder UseZenithView(this MauiAppBuilder builder)
    {
        return builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<ZenithView, ZenithViewHandler>());
    }
}
