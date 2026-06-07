using CommunityToolkit.Maui.Media;

namespace BlueChat.Services;

public class SttService
{
    private readonly ISpeechToText _stt;

    public SttService(ISpeechToText stt)
    {
        _stt = stt;
    }

    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        var granted = await _stt.RequestPermissions(ct);
        if (!granted) return null;

        var tcs = new TaskCompletionSource<string?>();

        void OnCompleted(object? s, SpeechToTextRecognitionResultCompletedEventArgs e)
            => tcs.TrySetResult(e.RecognitionResult.IsSuccessful ? e.RecognitionResult.Text : null);

        _stt.RecognitionResultCompleted += OnCompleted;

        try
        {
            await _stt.StartListeningAsync(System.Globalization.CultureInfo.CurrentCulture, ct);
            return await tcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _stt.RecognitionResultCompleted -= OnCompleted;
            await _stt.StopListeningAsync(CancellationToken.None);
        }
    }
}
