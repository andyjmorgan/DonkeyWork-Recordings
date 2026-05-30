using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DonkeyWork.Recordings.Audio.Core.Diagnostics;

// Central ActivitySource + Meter for the audio pipeline. The API registers these names
// with OpenTelemetry (AddSource / AddMeter).
public static class AudioDiagnostics
{
    public const string ActivitySourceName = "DonkeyWork.Recordings.Audio";
    public const string MeterName = "DonkeyWork.Recordings.Audio";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    // gpt-oss preprocessing (one measurement per prompt).
    public static readonly Histogram<double> PreprocessDuration =
        Meter.CreateHistogram<double>("tts.preprocess.duration", "s", "gpt-oss preprocessing wall-clock duration.");

    public static readonly Histogram<int> PreprocessParagraphs =
        Meter.CreateHistogram<int>("tts.preprocess.paragraphs", "{paragraph}", "Paragraphs returned by a gpt-oss prompt.");

    public static readonly Counter<long> PreprocessRequests =
        Meter.CreateCounter<long>("tts.preprocess.requests", "{request}", "gpt-oss prompts, tagged by outcome.");

    // Per-chunk TTS synthesis.
    public static readonly Histogram<double> ChunkSynthDuration =
        Meter.CreateHistogram<double>("tts.chunk.synth.duration", "s", "Per-chunk TTS synthesis duration.");

    public static readonly Counter<long> ChunksSynthesized =
        Meter.CreateCounter<long>("tts.chunks", "{chunk}", "Audio chunks synthesised.");

    // End-to-end recording generation.
    public static readonly Histogram<double> GenerationDuration =
        Meter.CreateHistogram<double>("tts.generation.duration", "s", "End-to-end recording generation duration.");

    public static readonly Counter<long> RecordingsGenerated =
        Meter.CreateCounter<long>("tts.recordings", "{recording}", "Recordings generated, tagged by outcome.");
}
