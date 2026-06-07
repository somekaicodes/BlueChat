using Android.App;
using Android.Content.PM;
using Android.OS;
using Plugin.BLE;

namespace BlueChat;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Let Plugin.BLE handle BLE permission requests on Android
        Plugin.BLE.Abstractions.Contracts.IAdapter adapter = CrossBluetoothLE.Current.Adapter;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        // Forward to Plugin.BLE so it knows permissions were granted
    }
}
