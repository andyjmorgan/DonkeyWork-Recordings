using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Core.Helpers;
using DonkeyWork.Recordings.Persistence.Entities.Tts;

namespace DonkeyWork.Recordings.Audio.Core.Mapping;

internal static class AudioMappings
{
    // Chunks/PlayableUpTo are populated only when the Chunks navigation was loaded (the single
    // recording GET does; list projections leave it empty to avoid an N+1).
    public static TtsRecordingV1 ToV1(this TtsRecordingEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        FilePath = entity.FilePath,
        Transcript = entity.Transcript,
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
        StatusDetail = entity.StatusDetail,
        ErrorMessage = entity.ErrorMessage,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        Chunks = entity.Chunks
            .OrderBy(c => c.Index)
            .Select(c => c.ToV1())
            .ToList(),
        PlayableUpTo = ChunkWatermark.Compute(entity.Chunks.Select(c => c.Index)),
    };

    public static TtsRecordingChunkV1 ToV1(this TtsRecordingChunkEntity entity) => new()
    {
        Index = entity.Index,
        Url = entity.FilePath,
        SizeBytes = entity.SizeBytes,
        DurationSeconds = entity.DurationSeconds,
        CreatedAt = entity.CreatedAt,
    };

    public static AudioCollectionV1 ToV1(this TtsAudioCollectionEntity entity, int recordingCount) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        CoverImagePath = entity.CoverImagePath,
        DefaultVoice = entity.DefaultVoice,
        DefaultLanguage = entity.DefaultLanguage,
        Author = entity.Author,
        AuthorEmail = entity.AuthorEmail,
        ItunesCategory = entity.ItunesCategory,
        IsExplicit = entity.IsExplicit,
        Link = entity.Link,
        RecordingCount = recordingCount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    public static BacklogItemV1 ToV1(this TtsBacklogItemEntity entity, string? consumedByRecordingName = null) => new()
    {
        Id = entity.Id,
        CollectionId = entity.CollectionId,
        Title = entity.Title,
        Content = entity.Content,
        SourceUrl = entity.SourceUrl,
        Notes = entity.Notes,
        Status = entity.Status.ToString(),
        ConsumedAt = entity.ConsumedAt,
        ConsumedByRecordingId = entity.ConsumedByRecordingId,
        ConsumedByRecordingName = consumedByRecordingName,
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
