using System.Collections.ObjectModel;
using BlueChat.Models;
using BlueChat.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueChat.ViewModels;

public partial class DiscoverViewModel : ObservableObject
{
    private readonly BleService _ble;
    private readonly IBlePeripheralService _peripheral;
    private readonly ChatViewModel _chat;
    private CancellationTokenSource? _scanCts;
    private string _pendingRemoteName = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "Tap Scan to find nearby BlueChat devices.";

    public ObservableCollection<NearbyDevice> NearbyDevices { get; } = [];

    public DiscoverViewModel(BleService ble, IBlePeripheralService peripheral, ChatViewModel chat)
    {
        _ble = ble;
        _peripheral = peripheral;
        _chat = chat;

        _ble.DeviceDiscovered += OnDeviceDiscovered;
        _ble.ConnectionResponseReceived += OnConnectionResponseReceived;
        _peripheral.ConnectionRequestReceived += OnConnectionRequestReceived;
    }

    // ── Scanning ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleScanAsync()
    {
        if (IsScanning)
        {
            _scanCts?.Cancel();
            await _ble.StopScanAsync();
            IsScanning = false;
            StatusMessage = "Scan stopped.";
            return;
        }

        // Check Bluetooth is on
        if (!_ble.IsBluetoothOn)
        {
            StatusMessage = "Bluetooth is off. Please enable it in Settings.";
            return;
        }

        // Request permissions
        if (!await RequestPermissionsAsync())
            return;

        NearbyDevices.Clear();
        IsScanning = true;
        StatusMessage = "Scanning...";
        _scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await _ble.StartScanAsync(_scanCts.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            StatusMessage = NearbyDevices.Count == 0
                ? "No devices found."
                : $"Found {NearbyDevices.Count} device(s). Tap Connect to start chatting.";
        }
    }

    private static async Task<bool> RequestPermissionsAsync()
    {
#if ANDROID
        // Location permission required for BLE scanning on Android
        var location = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (location != PermissionStatus.Granted)
        {
            await Shell.Current.DisplayAlertAsync(
                "Permission Required",
                "Location permission is needed to scan for nearby Bluetooth devices.",
                "OK");
            return false;
        }

        // Additional Bluetooth permissions on Android 12+
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var bt = await Permissions.RequestAsync<Permissions.Bluetooth>();
            if (bt != PermissionStatus.Granted)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Permission Required",
                    "Bluetooth permission is needed to scan for nearby devices.",
                    "OK");
                return false;
            }
        }
#endif
        // iOS handles Bluetooth permission automatically via CBCentralManager
        return true;
    }

    private void OnDeviceDiscovered(NearbyDevice device)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = NearbyDevices.FirstOrDefault(d => d.Id == device.Id);
            if (existing == null)
                NearbyDevices.Add(device);
            else
                existing.Rssi = device.Rssi;
        });
    }

    // ── Connection (Device A — initiator) ────────────────────────────────────

    [RelayCommand]
    private async Task ConnectToDeviceAsync(NearbyDevice device)
    {
        _pendingRemoteName = device.Name;
        StatusMessage = $"Sending request to {device.Name}...";

        try
        {
            var myName = Preferences.Default.Get("device_name", "Unknown");
            var ok = await _ble.SendConnectionRequestAsync(device, myName);
            StatusMessage = ok
                ? $"Request sent — waiting for {device.Name} to accept."
                : "Connection failed. Make sure the other device has BlueChat open.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection error: {ex.Message}";
        }
    }

    private void OnConnectionResponseReceived(bool accepted)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (accepted)
            {
                _chat.ConnectedDeviceName = _pendingRemoteName;
                _chat.Messages.Clear();
                await Shell.Current.GoToAsync("//Chat");
            }
            else
            {
                StatusMessage = $"{_pendingRemoteName} declined your request.";
            }
        });
    }

    // ── Connection (Device B — responder) ────────────────────────────────────

    private void OnConnectionRequestReceived(string remoteName)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var accept = await Shell.Current.DisplayAlertAsync(
                "Chat Request",
                $"{remoteName} wants to chat with you.",
                "Accept", "Decline");

            await _peripheral.SendResponseAsync(accept);

            if (accept)
            {
                _chat.ConnectedDeviceName = remoteName;
                _chat.Messages.Clear();
                await Shell.Current.GoToAsync("//Chat");
            }
            else
            {
                StatusMessage = $"Declined request from {remoteName}.";
            }
        });
    }
}
