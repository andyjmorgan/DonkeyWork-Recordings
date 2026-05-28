namespace DonkeyWork.Recordings.Audio.Contracts;

public static class NatsSubjects
{
    public const string AudioGenerationSubject = "audio.generate";

    public const string AudioGenerationStream = "wolverine-recordings-audio";

    public const string AudioGenerationConsumer = "recordings-audio-worker";
}
