using BlueChat.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace BlueChat.Services;

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
    private CancellationTokenSource? _keepAliveCts;

    public event Action<NearbyDevice>? DeviceDiscovered;
    public event Action<string>? MessageReceived;
    public event Action<bool>? ConnectionResponseReceived;

    public bool IsScanning => _adapter.IsScanning;
    public bool IsConnected => _connectedDevice != null;
    public bool IsBluetoothOn => _ble.State == BluetoothState.On;

    public BleService()
    {
        _ble = CrossBluetoothLE.Current;
        _adapter = CrossBluetoothLE.Current.Adapter;
        _adapter.ScanTimeout = 10_000;
        _adapter.ScanMode = ScanMode.LowLatency;

        _adapter.DeviceDiscovered += OnDeviceDiscovered;
        _adapter.DeviceDisconnected += OnDeviceDisconnected;
    }

    // ── Scanning ──────────────────────────────────────────────────────────────

    public async Task StartScanAsync(CancellationToken ct = default)
    {
        if (_ble.State != BluetoothState.On) return;
        await _adapter.StartScanningForDevicesAsync(serviceUuids: [ServiceUuid], cancellationToken: ct);
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

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        _keepAliveCts?.Cancel();
        _connectedDevice = null;
        _messageChar = null;
        _responseChar = null;
    }

    public async Task DisconnectAsync()
    {
        if (_connectedDevice != null)
            await _adapter.DisconnectDeviceAsync(_connectedDevice);
    }

    // ── Connection request + GATT setup ──────────────────────────────────────

    /// <summary>
    /// Connects to a nearby device, subscribes to response/message notifications,
    /// then writes our name to the RequestChar to initiate a chat request.
    /// </summary>
    public async Task<bool> SendConnectionRequestAsync(NearbyDevice device, string myName)
    {
        if (device.NativeDevice is not IDevice nativeDevice)
            return false;

        try
        {
            await _adapter.ConnectToDeviceAsync(nativeDevice);
            _connectedDevice = nativeDevice;

            // Brief delay to let the remote GATT server finish setup
            await Task.Delay(600);

            var service = await nativeDevice.GetServiceAsync(ServiceUuid);
            if (service == null) return false;

            // Subscribe to response and message notifications before sending the request
            _responseChar = await service.GetCharacteristicAsync(ResponseCharUuid);
            _messageChar  = await service.GetCharacteristicAsync(MessageCharUuid);

            if (_responseChar != null)
            {
                _responseChar.ValueUpdated += OnResponseReceived;
                await _responseChar.StartUpdatesAsync();
            }

            if (_messageChar != null)
            {
                _messageChar.ValueUpdated += OnMessageReceived;
                await _messageChar.StartUpdatesAsync();
            }

            var requestChar = await service.GetCharacteristicAsync(RequestCharUuid);
            if (requestChar == null) return false;

            var bytes = System.Text.Encoding.UTF8.GetBytes(myName);
            var result = await requestChar.WriteAsync(bytes) >= 0;

            if (result)
                StartKeepAlive();

            return result;
        }
        catch
        {
            _connectedDevice = null;
            return false;
        }
    }

    // ── Messaging ─────────────────────────────────────────────────────────────

    private void StartKeepAlive()
    {
        _keepAliveCts?.Cancel();
        _keepAliveCts = new CancellationTokenSource();
        var token = _keepAliveCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && _connectedDevice != null)
            {
                await Task.Delay(5000, token).ConfigureAwait(false);
                if (_messageChar != null && !token.IsCancellationRequested)
                {
                    try { await _messageChar.ReadAsync(token); }
                    catch { /* ignore read errors — connection drop handled by OnDeviceDisconnected */ }
                }
            }
        }, token);
    }

    public async Task<bool> SendMessageAsync(string text)
    {
        if (_messageChar == null) return false;
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return await _messageChar.WriteAsync(bytes) >= 0;
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
}
