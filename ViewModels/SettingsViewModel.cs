using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueChat.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string DeviceNameKey = "device_name";
    private const string DiscoverableKey = "discoverable";

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private bool _isDiscoverable;

    public SettingsViewModel()
    {
        _deviceName = Preferences.Default.Get(DeviceNameKey, "My Device");
        _isDiscoverable = Preferences.Default.Get(DiscoverableKey, false);
    }

    [RelayCommand]
    private void SaveDeviceName()
    {
        if (string.IsNullOrWhiteSpace(DeviceName)) return;
        Preferences.Default.Set(DeviceNameKey, DeviceName.Trim());
    }

    partial void OnIsDiscoverableChanged(bool value)
    {
        Preferences.Default.Set(DiscoverableKey, value);
        // TODO: start/stop BLE peripheral advertising
    }
}
