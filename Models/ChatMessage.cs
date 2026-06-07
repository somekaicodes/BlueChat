namespace BlueChat.Models;

public enum MessageDirection { Sent, Received }

public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public MessageDirection Direction { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public bool IsSent => Direction == MessageDirection.Sent;
    public bool IsReceived => Direction == MessageDirection.Received;
}
