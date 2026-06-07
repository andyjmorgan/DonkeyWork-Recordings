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
