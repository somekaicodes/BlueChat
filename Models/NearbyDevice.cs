namespace BlueChat.Models;

public class NearbyDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Rssi { get; set; }
    public object? NativeDevice { get; set; }

    public string SignalDisplay => Rssi == 0 ? "" : $"{Rssi} dBm";
}
