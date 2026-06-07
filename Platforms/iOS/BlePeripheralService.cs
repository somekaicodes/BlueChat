using BlueChat.Services;
using CoreBluetooth;
using Foundation;

namespace BlueChat.Platforms.iOS;

public class BlePeripheralService : IBlePeripheralService
{
    private CBPeripheralManager? _peripheralManager;
    private CBMutableCharacteristic? _responseChar;
    private CBMutableCharacteristic? _messageChar;
    private string _pendingDeviceName = string.Empty;
    private PeripheralDelegate? _delegate;

    public bool IsAdvertising => _peripheralManager?.Advertising ?? false;

    public event Action<string>? ConnectionRequestReceived;
    public event Action<string>? MessageReceived;

    public Task StartAdvertisingAsync(string deviceName)
    {
        _pendingDeviceName = deviceName;
        _delegate = new PeripheralDelegate(
            onRequest: name => ConnectionRequestReceived?.Invoke(name),
            onMessage: msg => MessageReceived?.Invoke(msg),
            onReady: StartAdvertisingInternal);

        _peripheralManager = new CBPeripheralManager(_delegate, null);
        return Task.CompletedTask;
    }

    private void StartAdvertisingInternal()
    {
        if (_peripheralManager == null) return;

        var serviceUuid = CBUUID.FromString(BleConstants.ServiceUuid);

        var requestChar = new CBMutableCharacteristic(
            CBUUID.FromString(BleConstants.RequestCharUuid),
            CBCharacteristicProperties.Write,
            null,
            CBAttributePermissions.Writeable);

        _responseChar = new CBMutableCharacteristic(
            CBUUID.FromString(BleConstants.ResponseCharUuid),
            CBCharacteristicProperties.Notify,
            null,
            CBAttributePermissions.Readable);

        _messageChar = new CBMutableCharacteristic(
            CBUUID.FromString(BleConstants.MessageCharUuid),
            CBCharacteristicProperties.Write | CBCharacteristicProperties.Notify,
            null,
            CBAttributePermissions.Readable | CBAttributePermissions.Writeable);

        var service = new CBMutableService(serviceUuid, true);
        service.Characteristics = [requestChar, _responseChar, _messageChar];
        _peripheralManager.AddService(service);

        _delegate!.PeripheralManager = _peripheralManager;

        _peripheralManager.StartAdvertising(new StartAdvertisingOptions
        {
            LocalName = _pendingDeviceName,
            ServicesUUID = [serviceUuid]
        });
    }

    public Task StopAdvertisingAsync()
    {
        _peripheralManager?.StopAdvertising();
        _peripheralManager?.RemoveAllServices();
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string text)
    {
        if (_messageChar == null || _peripheralManager == null) return Task.CompletedTask;
        var data = NSData.FromArray(System.Text.Encoding.UTF8.GetBytes(text));
        _peripheralManager.UpdateValue(data, _messageChar, null);
        return Task.CompletedTask;
    }

    public Task SendResponseAsync(bool accepted)
    {
        if (_responseChar == null || _peripheralManager == null) return Task.CompletedTask;
        var data = NSData.FromArray(System.Text.Encoding.UTF8.GetBytes(accepted ? "accept" : "decline"));
        _peripheralManager.UpdateValue(data, _responseChar, null);
        return Task.CompletedTask;
    }
}

// ── Delegate ──────────────────────────────────────────────────────────────────

class PeripheralDelegate(
    Action<string> onRequest,
    Action<string> onMessage,
    Action onReady) : CBPeripheralManagerDelegate
{
    public CBPeripheralManager? PeripheralManager { get; set; }

    public override void StateUpdated(CBPeripheralManager peripheral)
    {
        if (peripheral.State == CBManagerState.PoweredOn)
            onReady();
    }

    public override void WriteRequestsReceived(CBPeripheralManager peripheral, CBATTRequest[] requests)
    {
        foreach (var request in requests)
        {
            if (request.Value == null) continue;
            var text = System.Text.Encoding.UTF8.GetString(request.Value.ToArray());

            if (request.Characteristic.UUID.Uuid.Equals(BleConstants.RequestCharUuid, StringComparison.OrdinalIgnoreCase))
                onRequest(text);
            else if (request.Characteristic.UUID.Uuid.Equals(BleConstants.MessageCharUuid, StringComparison.OrdinalIgnoreCase))
                onMessage(text);

            peripheral.RespondToRequest(request, CBATTError.Success);
        }
    }

}
