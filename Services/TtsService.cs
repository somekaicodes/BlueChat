namespace BlueChat.Services;

public class TtsService
{
    private CancellationTokenSource? _cts;

    public async Task SpeakAsync(string text)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions
        {
            Volume = 1f,
            Pitch = 1f
        }, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }
}
