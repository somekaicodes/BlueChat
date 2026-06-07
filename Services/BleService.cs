using BlueChat.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace BlueChat.Services;

/// <summary>
/// Handles BLE scanning (Central) and advertising (Peripheral).
/// Each BlueChat device exposes one GATT service with three characteristics:
///   - RequestChar  (Write)        – incoming connection requests
///   - ResponseChar (Notify)       – accept/decline responses
///   - MessageChar  (Write+Notify) – chat messages
/// </summary>
public class BleService
{
    private static readonly Guid ServiceUuid      = Guid.Parse(BleConstants.ServiceUuid);
    private static readonly Guid RequestCharUuid  = Guid.Parse(BleConstants.RequestCharUuid);
    private static readonly Guid ResponseCharUuid = Guid.Parse(BleConstants.ResponseCharUuid);
    private static readonly Guid MessageCharUuid  = Guid.Parse(BleConstants.MessageCharUuid);

    private readonly IBluetoothLE _ble;
    private readonly IAdapter _adapter;

    private IDevice? _connectedDevice;
    private ICharacteristic? _messageChar;
    private ICharacteristic? _responseChar;

    public event Action<NearbyDevice>? DeviceDiscovered;
    public event Action<string>? MessageReceived;       // decoded text
    public event Action<string>? ConnectionRequested;   // remote device name
    public event Action<bool>? ConnectionResponseReceived; // true = accepted

    public bool IsScanning => _adapter.IsScanning;
    public bool IsConnected => _connectedDevice != null;

    public BleService()
    {
        _ble = CrossBluetoothLE.Current;
        _adapter = CrossBluetoothLE.Current.Adapter;
        _adapter.ScanTimeout = 10_000;
        _adapter.ScanMode = ScanMode.LowLatency;

        _adapter.DeviceDiscovered += OnDeviceDiscovered;
        _adapter.DeviceConnected += OnDeviceConnected;
        _adapter.DeviceDisconnected += OnDeviceDisconnected;
    }

    // ── Scanning ──────────────────────────────────────────────────────────────

    public async Task StartScanAsync(CancellationToken ct = default)
    {
        if (_ble.State != BluetoothState.On)
            return;

        await _adapter.StartScanningForDevicesAsync(
            serviceUuids: [ServiceUuid],
            cancellationToken: ct);
    }

    public async Task StopScanAsync()
        => await _adapter.StopScanningForDevicesAsync();

    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        var device = new NearbyDevice
        {
            Id = e.Device.Id.ToString(),
            Name = string.IsNullOrWhiteSpace(e.Device.Name) ? "Unknown" : e.Device.Name,
            Rssi = e.Device.Rssi,
            NativeDevice = e.Device
        };
        DeviceDiscovered?.Invoke(device);
    }

    // ── Connection ────────────────────────────────────────────────────────────

    public async Task<bool> ConnectAsync(NearbyDevice device)
    {
        if (device.NativeDevice is not IDevice nativeDevice)
            return false;

        try
        {
            await _adapter.ConnectToDeviceAsync(nativeDevice);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        _connectedDevice = e.Device;
        await SetupGattAsync(e.Device);
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        _connectedDevice = null;
        _messageChar = null;
        _responseChar = null;
    }

    public async Task DisconnectAsync()
    {
        if (_connectedDevice != null)
            await _adapter.DisconnectDeviceAsync(_connectedDevice);
    }

    // ── GATT setup ────────────────────────────────────────────────────────────

    private async Task SetupGattAsync(IDevice device)
    {
        try
        {
            var service = await device.GetServiceAsync(ServiceUuid);
            if (service == null) return;

            _messageChar = await service.GetCharacteristicAsync(MessageCharUuid);
            _responseChar = await service.GetCharacteristicAsync(ResponseCharUuid);

            if (_messageChar != null)
            {
                _messageChar.ValueUpdated += OnMessageReceived;
                await _messageChar.StartUpdatesAsync();
            }

            if (_responseChar != null)
            {
                _responseChar.ValueUpdated += OnResponseReceived;
                await _responseChar.StartUpdatesAsync();
            }
        }
        catch { /* device disconnected mid-setup */ }
    }

    private void OnMessageReceived(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var text = System.Text.Encoding.UTF8.GetString(e.Characteristic.Value);
        MessageReceived?.Invoke(text);
    }

    private void OnResponseReceived(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var text = System.Text.Encoding.UTF8.GetString(e.Characteristic.Value);
        ConnectionResponseReceived?.Invoke(text == "accept");
    }

    // ── Messaging ─────────────────────────────────────────────────────────────

    public async Task<bool> SendMessageAsync(string text)
    {
        if (_messageChar == null) return false;
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return await _messageChar.WriteAsync(bytes) >= 0;
    }

    // ── Request (sent before formal GATT chat) ────────────────────────────────

    public async Task<bool> SendConnectionRequestAsync(NearbyDevice device, string myName)
    {
        if (device.NativeDevice is not IDevice nativeDevice)
            return false;

        try
        {
            await _adapter.ConnectToDeviceAsync(nativeDevice);
            var service = await nativeDevice.GetServiceAsync(ServiceUuid);
            if (service == null) return false;

            var requestChar = await service.GetCharacteristicAsync(RequestCharUuid);
            if (requestChar == null) return false;

            var bytes = System.Text.Encoding.UTF8.GetBytes(myName);
            return await requestChar.WriteAsync(bytes) >= 0;
        }
        catch
        {
            return false;
        }
    }
}
