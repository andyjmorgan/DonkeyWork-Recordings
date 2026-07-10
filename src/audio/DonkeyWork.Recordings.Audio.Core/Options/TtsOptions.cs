namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class TtsOptions
{
    public const string SectionName = "Tts";

    public string DefaultLanguage { get; set; } = "en-US";

    // How often the background sweeper looks for settled recordings whose chunk clips can go.
    public TimeSpan ChunkSweepInterval { get; set; } = TimeSpan.FromMinutes(1);

    // How long chunk clips outlive a settled (Ready/Failed) recording before being swept, so a
    // client mid-playback on chunks has time to switch over to the final mp3.
    public TimeSpan ChunkSweepGracePeriod { get; set; } = TimeSpan.FromMinutes(5);
}
