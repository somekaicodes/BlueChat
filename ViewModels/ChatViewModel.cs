using System.Collections.ObjectModel;
using BlueChat.Models;
using BlueChat.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueChat.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly BleService _ble;
    private readonly IBlePeripheralService _peripheral;
    private readonly TtsService _tts;
    private readonly SttService _stt;

    [ObservableProperty]
    private string _messageText = string.Empty;

    [ObservableProperty]
    private bool _ttsEnabled;

    [ObservableProperty]
    private bool _isListening;

    [ObservableProperty]
    private string _connectedDeviceName = "Chat";

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ChatViewModel(BleService ble, IBlePeripheralService peripheral, TtsService tts, SttService stt)
    {
        _ble = ble;
        _peripheral = peripheral;
        _tts = tts;
        _stt = stt;

        // Central path: Device A receives messages via GATT notifications from Device B
        _ble.MessageReceived += OnMessageReceived;

        // Peripheral path: Device B receives messages when Device A writes to its characteristic
        _peripheral.MessageReceived += OnMessageReceived;
    }

    [RelayCommand]
    private void ToggleTts() => TtsEnabled = !TtsEnabled;

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = MessageText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        bool sent;
        if (_ble.IsConnected)
            sent = await _ble.SendMessageAsync(text);
        else
        {
            await _peripheral.SendMessageAsync(text);
            sent = true;
        }

        if (sent)
        {
            Messages.Add(new ChatMessage { Text = text, Direction = MessageDirection.Sent });
            MessageText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task StartListeningAsync()
    {
        if (IsListening) return;
        IsListening = true;
        var result = await _stt.ListenAsync();
        if (!string.IsNullOrWhiteSpace(result))
            MessageText = result;
        IsListening = false;
    }

    private void OnMessageReceived(string text)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            Messages.Add(new ChatMessage { Text = text, Direction = MessageDirection.Received });
            if (TtsEnabled)
                await _tts.SpeakAsync(text);
        });
    }
}
