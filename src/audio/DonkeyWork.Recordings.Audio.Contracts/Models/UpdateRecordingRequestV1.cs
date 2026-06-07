namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class UpdateRecordingRequestV1
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? ChapterTitle { get; init; }
}
