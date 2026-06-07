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

        // Device A: remote accepted/declined our request
        _ble.ConnectionResponseReceived += OnConnectionResponseReceived;

        // Device B: someone wants to chat with us
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
                    : $"Found {NearbyDevices.Count} device(s). Tap Connect to start chatting.";
            }
        }
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

        var myName = Preferences.Default.Get("device_name", "Unknown");
        var ok = await _ble.SendConnectionRequestAsync(device, myName);

        StatusMessage = ok
            ? $"Request sent — waiting for {device.Name} to accept."
            : "Connection failed. Make sure the other device has BlueChat open.";
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
