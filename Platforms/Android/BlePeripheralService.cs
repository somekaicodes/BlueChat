using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using BlueChat.Services;
using Java.Util;

namespace BlueChat.Platforms.Android;

public class BlePeripheralService : IBlePeripheralService
{
    private BluetoothLeAdvertiser? _advertiser;
    private BluetoothGattServer? _gattServer;
    private BlueChatAdvertiseCallback? _advertiseCallback;
    private BlueChatGattServerCallback? _gattCallback;

    private BluetoothGattCharacteristic? _responseChar;
    private BluetoothGattCharacteristic? _messageChar;
    private readonly List<BluetoothDevice> _subscribedDevices = [];

    public bool IsAdvertising { get; private set; }

    public event Action<string>? ConnectionRequestReceived;
    public event Action<string>? MessageReceived;

    public Task StartAdvertisingAsync(string deviceName)
    {
        var manager = (BluetoothManager)Platform.AppContext.GetSystemService(Context.BluetoothService)!;
        var adapter = manager.Adapter!;

        // Build GATT server
        _gattCallback = new BlueChatGattServerCallback(
            onRequest: name => ConnectionRequestReceived?.Invoke(name),
            onMessage: msg => MessageReceived?.Invoke(msg),
            onSubscribe: device => { if (!_subscribedDevices.Contains(device)) _subscribedDevices.Add(device); },
            onUnsubscribe: device => _subscribedDevices.Remove(device));

        _gattServer = manager.OpenGattServer(Platform.AppContext, _gattCallback);
        _gattCallback.GattServer = _gattServer;

        var service = new BluetoothGattService(
            UUID.FromString(BleConstants.ServiceUuid),
            GattServiceType.Primary)!;

        var requestChar = new BluetoothGattCharacteristic(
            UUID.FromString(BleConstants.RequestCharUuid),
            GattProperty.Write,
            GattPermission.Write)!;

        _responseChar = new BluetoothGattCharacteristic(
            UUID.FromString(BleConstants.ResponseCharUuid),
            GattProperty.Notify,
            GattPermission.Read)!;

        _messageChar = new BluetoothGattCharacteristic(
            UUID.FromString(BleConstants.MessageCharUuid),
            GattProperty.Write | GattProperty.Notify,
            GattPermission.Write | GattPermission.Read)!;

        var cccdUuid = UUID.FromString("00002902-0000-1000-8000-00805f9b34fb")!;

        var responseDescriptor = new BluetoothGattDescriptor(cccdUuid, GattDescriptorPermission.Read | GattDescriptorPermission.Write)!;
        _responseChar.AddDescriptor(responseDescriptor);

        var messageDescriptor = new BluetoothGattDescriptor(cccdUuid, GattDescriptorPermission.Read | GattDescriptorPermission.Write)!;
        _messageChar.AddDescriptor(messageDescriptor);

        service.AddCharacteristic(requestChar);
        service.AddCharacteristic(_responseChar);
        service.AddCharacteristic(_messageChar);
        _gattServer.AddService(service);

        // Start advertising
        _advertiser = adapter.BluetoothLeAdvertiser!;
        _advertiseCallback = new BlueChatAdvertiseCallback();

        var settings = new AdvertiseSettings.Builder()!
            .SetAdvertiseMode(AdvertiseMode.LowLatency)!
            .SetConnectable(true)!
            .SetTxPowerLevel(AdvertiseTx.PowerHigh)!
            .Build()!;

        var data = new AdvertiseData.Builder()!
            .SetIncludeDeviceName(true)!
            .AddServiceUuid(new ParcelUuid(UUID.FromString(BleConstants.ServiceUuid)!))!
            .Build()!;

        _advertiser.StartAdvertising(settings, data, _advertiseCallback);
        IsAdvertising = true;
        return Task.CompletedTask;
    }

    public Task StopAdvertisingAsync()
    {
        if (_advertiseCallback != null)
            _advertiser?.StopAdvertising(_advertiseCallback);
        _gattServer?.Close();
        _subscribedDevices.Clear();
        IsAdvertising = false;
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string text)
    {
        if (_messageChar == null || _gattServer == null) return Task.CompletedTask;
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        _messageChar.SetValue(bytes);
        foreach (var device in _subscribedDevices)
            _gattServer.NotifyCharacteristicChanged(device, _messageChar, false);
        return Task.CompletedTask;
    }

    public Task SendResponseAsync(bool accepted)
    {
        if (_responseChar == null || _gattServer == null) return Task.CompletedTask;
        var bytes = System.Text.Encoding.UTF8.GetBytes(accepted ? "accept" : "decline");
        _responseChar.SetValue(bytes);
        foreach (var device in _subscribedDevices)
            _gattServer.NotifyCharacteristicChanged(device, _responseChar, false);
        return Task.CompletedTask;
    }
}

// ── Callbacks ─────────────────────────────────────────────────────────────────

class BlueChatAdvertiseCallback : AdvertiseCallback { }

class BlueChatGattServerCallback(
    Action<string> onRequest,
    Action<string> onMessage,
    Action<BluetoothDevice> onSubscribe,
    Action<BluetoothDevice> onUnsubscribe) : BluetoothGattServerCallback
{
    public BluetoothGattServer? GattServer { get; set; }

    public override void OnCharacteristicWriteRequest(
        BluetoothDevice? device, int requestId,
        BluetoothGattCharacteristic? characteristic,
        bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
    {
        if (device == null || characteristic == null || value == null) return;

        GattServer?.SendResponse(device, requestId, GattStatus.Success, 0, value);

        var text = System.Text.Encoding.UTF8.GetString(value);

        if (characteristic.Uuid?.ToString()?.ToLower() == BleConstants.RequestCharUuid.ToLower())
            onRequest(text);
        else if (characteristic.Uuid?.ToString()?.ToLower() == BleConstants.MessageCharUuid.ToLower())
            onMessage(text);
    }

    public override void OnDescriptorWriteRequest(
        BluetoothDevice? device, int requestId,
        BluetoothGattDescriptor? descriptor,
        bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
    {
        if (device == null || value == null) return;

        GattServer?.SendResponse(device, requestId, GattStatus.Success, 0, value);

        if (value.SequenceEqual(BluetoothGattDescriptor.EnableNotificationValue?.ToArray() ?? []))
            onSubscribe(device);
        else if (value.SequenceEqual(BluetoothGattDescriptor.DisableNotificationValue?.ToArray() ?? []))
            onUnsubscribe(device);
    }

    public override void OnConnectionStateChange(BluetoothDevice? device, ProfileState status, ProfileState newState)
    {
        if (device != null && newState == ProfileState.Disconnected)
            onUnsubscribe(device);
    }
}
