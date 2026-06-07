namespace DonkeyWork.Recordings.Audio.Contracts.Messages;

public sealed record GenerateAudioRecordingCommand(
    Guid RecordingId,
    Guid UserId,
    IReadOnlyList<string> Paragraphs,
    string Voice,
    string Language,
    int TargetCharCount,
    int MaxCharCount);
