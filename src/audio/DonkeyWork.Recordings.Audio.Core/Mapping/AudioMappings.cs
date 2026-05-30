using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Persistence.Entities.Tts;

namespace DonkeyWork.Recordings.Audio.Core.Mapping;

internal static class AudioMappings
{
    public static TtsRecordingV1 ToV1(this TtsRecordingEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        FilePath = entity.FilePath,
        Transcript = entity.Transcript,
        ProcessedTranscript = entity.ProcessedTranscript,
        TtsModel = entity.TtsModel,
        ContentType = entity.ContentType,
        SizeBytes = entity.SizeBytes,
        DurationSeconds = entity.DurationSeconds,
        Voice = entity.Voice,
        Language = entity.Language,
        CollectionId = entity.CollectionId,
        SequenceNumber = entity.SequenceNumber,
        ChapterTitle = entity.ChapterTitle,
        Status = entity.Status.ToString(),
        Progress = entity.Progress,
        ErrorMessage = entity.ErrorMessage,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    public static AudioCollectionV1 ToV1(this TtsAudioCollectionEntity entity, int recordingCount) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        CoverImagePath = entity.CoverImagePath,
        DefaultTtsModel = entity.DefaultTtsModel,
        DefaultVoice = entity.DefaultVoice,
        DefaultLanguage = entity.DefaultLanguage,
        Tone = entity.Tone,
        Author = entity.Author,
        AuthorEmail = entity.AuthorEmail,
        ItunesCategory = entity.ItunesCategory,
        IsExplicit = entity.IsExplicit,
        Link = entity.Link,
        RecordingCount = recordingCount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    public static TtsPlaybackV1 ToV1(this TtsPlaybackEntity entity) => new()
    {
        PositionSeconds = entity.PositionSeconds,
        DurationSeconds = entity.DurationSeconds,
        Completed = entity.Completed,
        PlaybackSpeed = entity.PlaybackSpeed,
        UpdatedAt = entity.UpdatedAt,
    };
}
