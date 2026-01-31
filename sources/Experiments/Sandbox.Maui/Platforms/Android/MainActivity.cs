using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using AndroidX.Core.View;

namespace Sandbox.Maui.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.UserLandscape, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Window?.Attributes?.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;

        WindowInsetsControllerCompat? compat = WindowCompat.GetInsetsController(Window, Window?.DecorView);
        compat?.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        compat?.Hide(WindowInsetsCompat.Type.SystemBars());

        EdgeToEdge.Enable(this);
    }
}
