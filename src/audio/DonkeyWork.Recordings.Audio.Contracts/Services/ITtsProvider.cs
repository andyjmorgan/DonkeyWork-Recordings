namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface ITtsProvider
{
    Task<TtsClipResult> SynthesizeAsync(string text, TtsProviderRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TtsVoice>> ListVoicesAsync(CancellationToken cancellationToken = default);
}

public sealed record TtsProviderRequest(string Voice, string Language, int SampleRateHz);

public sealed record TtsClipResult(byte[] Audio, string ContentType, int SampleRateHz);

public sealed record TtsVoice(string Id, string Language, string Name, string? Emotion);
