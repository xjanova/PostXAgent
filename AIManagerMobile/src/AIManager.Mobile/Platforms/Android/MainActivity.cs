using Android.App;
using Android.Content.PM;
using Android.OS;

namespace AIManager.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                          ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    LaunchMode = LaunchMode.SingleTop)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Request permissions on app start
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            RequestPermissions(new[]
            {
                Android.Manifest.Permission.PostNotifications,
                Android.Manifest.Permission.ReceiveSms,
                Android.Manifest.Permission.ReadSms
            }, 0);
        }
        else
        {
            RequestPermissions(new[]
            {
                Android.Manifest.Permission.ReceiveSms,
                Android.Manifest.Permission.ReadSms
            }, 0);
        }
    }
}
