namespace DonkeyWork.Recordings.Audio.Core.Helpers;

// Pure lookup tables for the OpenAI-compatible surface (`/openai/v1/*`) so an unmodified OpenAI
// TTS client can talk to this service. Kept free of I/O so they are trivially unit-testable.
public static class OpenAiCompatibility
{
    // The single model this service exposes through the compatibility surface.
    public const string ModelId = "kokoro";

    public const string ModelOwner = "donkeywork";

    // Stable `created` timestamp for the model object — the repo's first commit
    // (2026-05-28T21:51:41Z). OpenAI reports a fixed unix-seconds value per model; ours must be
    // equally stable across calls and deployments.
    public const long ModelCreatedUnixSeconds = 1780005101;

    // OpenAI's documented limit for POST /v1/audio/speech `input`.
    public const int MaxInputChars = 4096;

    public const double MinSpeed = 0.25;

    public const double MaxSpeed = 4.0;

    // OpenAI voice name → Kokoro voice id.
    //
    // Five names have an exact Kokoro namesake (alloy, echo, fable, nova, onyx). The remaining four
    // have no Kokoro counterpart, so they map to the closest well-graded voice from Kokoro's
    // published quality grades (see KokoroTtsProvider.Ratings):
    //   ash     → am_michael (C+ — the best-graded American male; ash is a deep male voice)
    //   coral   → af_bella   (A-  — warm American female, matching coral's warm tone)
    //   sage    → af_sarah   (C+  — measured American female, matching sage's calm delivery)
    //   shimmer → af_heart   (A   — the best-graded voice overall; shimmer is a bright female voice)
    private static readonly IReadOnlyDictionary<string, string> VoiceAliasMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["alloy"] = "af_alloy",
            ["ash"] = "am_michael",
            ["coral"] = "af_bella",
            ["echo"] = "am_echo",
            ["fable"] = "bm_fable",
            ["nova"] = "af_nova",
            ["onyx"] = "am_onyx",
            ["sage"] = "af_sarah",
            ["shimmer"] = "af_heart",
        };

    public static IReadOnlyDictionary<string, string> VoiceAliases => VoiceAliasMap;

    // The six response_format values OpenAI accepts, with the content type api.openai.com serves
    // for each (captured live: mp3→audio/mpeg, opus→audio/opus, aac→audio/aac, flac→audio/flac,
    // wav→audio/wav, pcm→audio/pcm).
    private static readonly IReadOnlyDictionary<string, string> FormatContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mp3"] = "audio/mpeg",
            ["opus"] = "audio/opus",
            ["aac"] = "audio/aac",
            ["flac"] = "audio/flac",
            ["wav"] = "audio/wav",
            ["pcm"] = "audio/pcm",
        };

    public static bool TryResolveVoiceAlias(string voice, out string kokoroVoiceId)
        => VoiceAliasMap.TryGetValue(voice, out kokoroVoiceId!);

    public static bool TryGetContentType(string responseFormat, out string contentType)
        => FormatContentTypes.TryGetValue(responseFormat, out contentType!);

    // Mirrors OpenAI's enum-validation phrasing for the response_format field.
    public const string ResponseFormatEnumMessage =
        "Input should be 'mp3', 'aac', 'opus', 'flac', 'pcm' or 'wav'";

    // Convert a (concatenated) WAV clip to the requested response_format via ffmpeg. `wav` is a
    // pass-through since the synth backend already produces RIFF/WAVE PCM.
    public static byte[] ConvertWav(byte[] wavBytes, string responseFormat)
        => responseFormat.ToLowerInvariant() switch
        {
            "mp3" => AudioConverter.WavToMp3(wavBytes),
            "aac" => AudioConverter.WavToAac(wavBytes),
            "opus" => AudioConverter.WavToOggOpus(wavBytes),
            "flac" => AudioConverter.WavToFlac(wavBytes),
            "pcm" => AudioConverter.WavToPcm(wavBytes),
            "wav" => wavBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(responseFormat), responseFormat, "Unsupported response format."),
        };
}
