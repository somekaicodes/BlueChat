namespace BlueChat.Services;

/// <summary>Shared BLE UUID constants used by both Central and Peripheral.</summary>
public static class BleConstants
{
    public const string ServiceUuid      = "12345678-1234-1234-1234-1234567890ab";
    public const string RequestCharUuid  = "12345678-1234-1234-1234-1234567890ac";
    public const string ResponseCharUuid = "12345678-1234-1234-1234-1234567890ad";
    public const string MessageCharUuid  = "12345678-1234-1234-1234-1234567890ae";
}
