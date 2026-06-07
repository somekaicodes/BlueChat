using CommunityToolkit.Maui.Media;

namespace BlueChat.Services;

public class SttService
{
    private readonly ISpeechToText _stt;
    private CancellationTokenSource? _cts;

    public SttService(ISpeechToText stt)
    {
        _stt = stt;
    }

    public async Task<string?> ListenAsync(Action<string>? onPartialResult = null, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var granted = await _stt.RequestPermissions(_cts.Token);
        if (!granted) return null;

        var tcs = new TaskCompletionSource<string?>();

        void OnUpdated(object? s, SpeechToTextRecognitionResultUpdatedEventArgs e)
            => onPartialResult?.Invoke(e.RecognitionResult);

        void OnCompleted(object? s, SpeechToTextRecognitionResultCompletedEventArgs e)
            => tcs.TrySetResult(e.RecognitionResult.IsSuccessful ? e.RecognitionResult.Text : null);

        _stt.RecognitionResultUpdated += OnUpdated;
        _stt.RecognitionResultCompleted += OnCompleted;

        try
        {
            await _stt.StartListenAsync(
                new SpeechToTextOptions { Culture = System.Globalization.CultureInfo.CurrentCulture },
                _cts.Token);

            return await tcs.Task.WaitAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _stt.RecognitionResultUpdated -= OnUpdated;
            _stt.RecognitionResultCompleted -= OnCompleted;
            await _stt.StopListenAsync(CancellationToken.None);
        }
    }

    public void StopListening()
    {
        _cts?.Cancel();
    }
}
