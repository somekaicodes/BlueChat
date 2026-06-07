namespace BlueChat.Services;

public interface IBlePeripheralService
{
    bool IsAdvertising { get; }

    Task StartAdvertisingAsync(string deviceName);
    Task StopAdvertisingAsync();

    /// <summary>Fires when a remote device writes to the request characteristic.</summary>
    event Action<string> ConnectionRequestReceived;

    /// <summary>Fires when a remote device sends a chat message.</summary>
    event Action<string> MessageReceived;

    /// <summary>Send a message to all subscribed centrals.</summary>
    Task SendMessageAsync(string text);

    /// <summary>Send an accept/decline response.</summary>
    Task SendResponseAsync(bool accepted);
}
