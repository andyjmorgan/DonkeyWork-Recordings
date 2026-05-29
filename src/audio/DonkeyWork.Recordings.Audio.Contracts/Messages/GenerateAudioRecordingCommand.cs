namespace DonkeyWork.Recordings.Audio.Contracts.Messages;

public sealed record GenerateAudioRecordingCommand(
    Guid RecordingId,
    Guid UserId,
    string Text,
    string Voice,
    string Language,
    int TargetCharCount,
    int MaxCharCount);
