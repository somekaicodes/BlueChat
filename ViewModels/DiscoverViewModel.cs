using System.Collections.ObjectModel;
using BlueChat.Models;
using BlueChat.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueChat.ViewModels;

public partial class DiscoverViewModel : ObservableObject
{
    private readonly BleService _ble;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "Tap Scan to find nearby BlueChat devices.";

    public ObservableCollection<NearbyDevice> NearbyDevices { get; } = [];

    public DiscoverViewModel(BleService ble)
    {
        _ble = ble;
        _ble.DeviceDiscovered += OnDeviceDiscovered;
        _ble.ConnectionRequested += OnConnectionRequested;
    }

    [RelayCommand]
    private async Task ToggleScanAsync()
    {
        if (IsScanning)
        {
            _scanCts?.Cancel();
            await _ble.StopScanAsync();
            IsScanning = false;
            StatusMessage = "Scan stopped.";
        }
        else
        {
            NearbyDevices.Clear();
            IsScanning = true;
            StatusMessage = "Scanning...";
            _scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                await _ble.StartScanAsync(_scanCts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsScanning = false;
                StatusMessage = NearbyDevices.Count == 0
                    ? "No devices found."
                    : $"Found {NearbyDevices.Count} device(s).";
            }
        }
    }

    [RelayCommand]
    private async Task ConnectToDeviceAsync(NearbyDevice device)
    {
        StatusMessage = $"Connecting to {device.Name}...";
        var myName = Preferences.Default.Get("device_name", "Unknown");
        var ok = await _ble.SendConnectionRequestAsync(device, myName);
        StatusMessage = ok ? $"Request sent to {device.Name}." : "Connection failed.";
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

    private async void OnConnectionRequested(string remoteName)
    {
        var accept = await Shell.Current.DisplayAlertAsync(
            "Chat Request",
            $"{remoteName} wants to chat with you.",
            "Accept", "Decline");

        // TODO: send accept/decline response via BLE ResponseChar
        StatusMessage = accept ? $"Connected to {remoteName}." : "Request declined.";
    }
}
