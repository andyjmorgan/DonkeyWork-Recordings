namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface ITtsProvider
{
    string Key { get; }

    string DisplayName { get; }

    bool SupportsVoiceSelection { get; }

    string DefaultVoice { get; }

    Task<TtsClipResult> SynthesizeAsync(string text, TtsProviderRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TtsVoice>> ListVoicesAsync(CancellationToken cancellationToken = default);
}

public sealed record TtsProviderRequest(string Voice, string Language);

public sealed record TtsClipResult(byte[] Audio, string ContentType, int SampleRateHz);

public sealed record TtsVoice(string Id, string Language, string Name, string? Emotion);
