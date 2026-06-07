using BlueChat.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueChat.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string DeviceNameKey = "device_name";
    private const string DiscoverableKey = "discoverable";

    private readonly IBlePeripheralService _peripheral;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private bool _isDiscoverable;

    public SettingsViewModel(IBlePeripheralService peripheral)
    {
        _peripheral = peripheral;
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

        var name = Preferences.Default.Get(DeviceNameKey, "My Device");
        if (value)
            _ = _peripheral.StartAdvertisingAsync(name);
        else
            _ = _peripheral.StopAdvertisingAsync();
    }
}
