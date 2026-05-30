using System.ComponentModel;
using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace DonkeyWork.Recordings.Audio.Api.McpTools;

[McpServerToolType]
[McpToolProvider(Provider = McpToolProvider.DonkeyWork)]
public class AudioTools
{
    private readonly ITtsService _ttsService;
    private readonly IAudioCollectionService _collectionService;
    private readonly IAudioGenerationService _generationService;
    private readonly ITtsProviderRegistry _ttsRegistry;

    public AudioTools(
        ITtsService ttsService,
        IAudioCollectionService collectionService,
        IAudioGenerationService generationService,
        ITtsProviderRegistry ttsRegistry)
    {
        _ttsService = ttsService;
        _collectionService = collectionService;
        _generationService = generationService;
        _ttsRegistry = ttsRegistry;
    }

    [McpServerTool(Name = "create_audio_recording", Title = "Create Audio Recording")]
    [Description(
        "Submit text to be synthesised into a podcast-style audio recording in a channel. Every recording belongs to a " +
        "channel (collectionId is REQUIRED) — create one first with create_audio_collection if needed. Voice/model/" +
        "language come from the channel by default and are optional to override. Returns the recording immediately with " +
        "Status=Pending — the id is the job id. The TTS pipeline runs in the background; poll get_audio_recording until " +
        "Status is Ready or Failed.")]
    public async Task<TtsRecordingV1?> CreateAudioRecording(
        [Description("The text to synthesize. Markdown is fine; the preprocessor strips it.")] string text,
        [Description("Display name for the recording (shown in the UI and used in the RSS title fallback).")] string name,
        [Description("REQUIRED. The channel (collection) id this recording belongs to. Tone and the default voice/model are inherited from the channel.")] Guid collectionId,
        [Description("Optional override of the channel's TTS model — a key from list_tts_models (e.g. 'kokoro', 'chatterbox'). Omit to use the channel default.")] string? ttsModel = null,
        [Description("Optional override of the channel's voice — a voice id from list_tts_models for the chosen model (only models with SupportsVoiceSelection use it). Omit to use the channel default.")] string? voice = null,
        [Description("Optional override of the channel's language (BCP-47, e.g. en-US). Omit to use the channel default.")] string? language = null,
        [Description("Optional 1-based position within the channel. Auto-assigned (max + 1) if omitted.")] int? sequenceNumber = null,
        [Description("Optional chapter-style title within the channel (falls back to Name in the UI).")] string? chapterTitle = null,
        [Description("Optional description / show-notes.")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        var recordingId = await _generationService.StartGenerationAsync(new StartAudioGenerationRequestV1
        {
            Text = text,
            Name = name,
            Description = description,
            TtsModel = ttsModel,
            Voice = voice,
            Language = language,
            CollectionId = collectionId,
            SequenceNumber = sequenceNumber,
            ChapterTitle = chapterTitle,
        }, cancellationToken);

        return await _ttsService.GetRecordingAsync(recordingId, cancellationToken);
    }

    [McpServerTool(Name = "list_tts_models", Title = "List TTS Models", ReadOnly = true)]
    [Description(
        "List the available TTS models and, for models with SupportsVoiceSelection, their voices. " +
        "Use a model's Key as ttsModel and a voice's Id as voice when creating a recording.")]
    public async Task<IReadOnlyList<TtsModelInfo>> ListTtsModels(CancellationToken cancellationToken = default)
    {
        var models = new List<TtsModelInfo>();
        foreach (var provider in _ttsRegistry.Providers)
        {
            IReadOnlyList<TtsVoice> voices;
            try
            {
                voices = await provider.ListVoicesAsync(cancellationToken);
            }
            catch
            {
                voices = [];
            }

            models.Add(new TtsModelInfo(
                provider.Key,
                provider.DisplayName,
                provider.SupportsVoiceSelection,
                provider.DefaultVoice,
                provider.Key == _ttsRegistry.Default.Key,
                voices));
        }

        return models;
    }

    public sealed record TtsModelInfo(
        string Key,
        string DisplayName,
        bool SupportsVoiceSelection,
        string DefaultVoice,
        bool IsDefault,
        IReadOnlyList<TtsVoice> Voices);

    [McpServerTool(Name = "get_audio_recording", Title = "Get Audio Recording", ReadOnly = true)]
    [Description(
        "Get a single audio recording by ID. Returns full metadata: Status (Pending/Generating/Ready/Failed), " +
        "Progress (0-1) and StatusDetail (a human-readable stage like 'Preparing the script…' or " +
        "'Generating audio — segment 3 of 9') for live progress while Generating, FilePath (public mp3 URL " +
        "when Ready), DurationSeconds, transcript, etc.")]
    public Task<TtsRecordingV1?> GetAudioRecording(
        [Description("The recording id.")] Guid recordingId,
        CancellationToken cancellationToken = default)
    {
        return _ttsService.GetRecordingAsync(recordingId, cancellationToken);
    }

    [McpServerTool(Name = "list_audio_recordings", Title = "List Audio Recordings", ReadOnly = true)]
    [Description(
        "List audio recordings. Pass collectionId to scope to one channel (ordered by sequence). " +
        "Pass unfiledOnly=true for recordings not assigned to any channel. " +
        "Omit both for a flat list of every recording, newest first.")]
    public async Task<ListRecordingsResponseV1?> ListAudioRecordings(
        [Description("Optional channel id to scope to one channel.")] Guid? collectionId = null,
        [Description("If true, only return recordings not assigned to any channel. Ignored when collectionId is set.")] bool unfiledOnly = false,
        [Description("Pagination offset (default 0).")] int? offset = null,
        [Description("Page size (default 20, max 100).")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var pageOffset = offset ?? 0;
        var pageLimit = Math.Min(limit ?? 20, 100);

        if (collectionId.HasValue)
        {
            return await _collectionService.ListRecordingsAsync(collectionId.Value, pageOffset, pageLimit, cancellationToken);
        }

        return await _ttsService.ListRecordingsAsync(pageOffset, pageLimit, unfiledOnly, cancellationToken);
    }

    [McpServerTool(Name = "delete_audio_recording", Title = "Delete Audio Recording")]
    [Description("Permanently delete an audio recording and its underlying mp3 in object storage.")]
    public Task<bool> DeleteAudioRecording(
        [Description("The recording id.")] Guid recordingId,
        CancellationToken cancellationToken = default)
    {
        return _ttsService.DeleteRecordingAsync(recordingId, cancellationToken);
    }

    [McpServerTool(Name = "move_audio_recording", Title = "Move Audio Recording")]
    [Description("Move a recording to a different channel. A recording always belongs to a channel, so a target channel is required.")]
    public Task<TtsRecordingV1?> MoveAudioRecording(
        [Description("The recording id.")] Guid recordingId,
        [Description("REQUIRED. The target channel id to move the recording into.")] Guid collectionId,
        [Description("Optional 1-based sequence number within the target channel. Auto-assigned if omitted.")] int? sequenceNumber = null,
        [Description("Optional chapter title within the target channel.")] string? chapterTitle = null,
        CancellationToken cancellationToken = default)
    {
        return _ttsService.MoveRecordingAsync(recordingId, new MoveRecordingToCollectionRequestV1
        {
            CollectionId = collectionId,
            SequenceNumber = sequenceNumber,
            ChapterTitle = chapterTitle,
        }, cancellationToken);
    }

    [McpServerTool(Name = "list_audio_collections", Title = "List Audio Collections", ReadOnly = true)]
    [Description("List audio collections (channels) for the current user.")]
    public Task<ListAudioCollectionsResponseV1> ListAudioCollections(
        [Description("Pagination offset (default 0).")] int? offset = null,
        [Description("Page size (default 20, max 100).")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return _collectionService.ListAsync(offset ?? 0, Math.Min(limit ?? 20, 100), cancellationToken);
    }

    [McpServerTool(Name = "get_audio_collection", Title = "Get Audio Collection", ReadOnly = true)]
    [Description("Get a single audio collection (channel) by id.")]
    public Task<AudioCollectionV1?> GetAudioCollection(
        [Description("Channel id.")] Guid id,
        CancellationToken cancellationToken = default)
    {
        return _collectionService.GetAsync(id, cancellationToken);
    }

    [McpServerTool(Name = "create_audio_collection", Title = "Create Audio Collection")]
    [Description("Create a new audio collection (channel) scoped to the current user.")]
    public Task<AudioCollectionV1> CreateAudioCollection(
        [Description("Display name for the channel.")] string name,
        [Description("Optional description.")] string? description = null,
        [Description("Optional default TTS model key (from list_tts_models) for new recordings in this channel.")] string? defaultTtsModel = null,
        [Description("Optional default voice (for a model that supports voice selection) for new recordings in this channel.")] string? defaultVoice = null,
        [Description("Optional default BCP-47 language code for new recordings in this channel.")] string? defaultLanguage = null,
        [Description("Optional free-text tone instruction passed to gpt-oss for every recording in this channel (e.g. 'serious news anchor', 'upbeat morning host').")] string? tone = null,
        CancellationToken cancellationToken = default)
    {
        return _collectionService.CreateAsync(new CreateAudioCollectionRequestV1
        {
            Name = name,
            Description = description,
            DefaultTtsModel = defaultTtsModel,
            DefaultVoice = defaultVoice,
            DefaultLanguage = defaultLanguage,
            Tone = tone,
        }, cancellationToken);
    }

    [McpServerTool(Name = "update_audio_collection", Title = "Update Audio Collection")]
    [Description("Patch-update an existing channel. Omit fields to leave them unchanged.")]
    public Task<AudioCollectionV1?> UpdateAudioCollection(
        [Description("Channel id.")] Guid id,
        [Description("New name.")] string? name = null,
        [Description("New description.")] string? description = null,
        [Description("New default TTS model key (from list_tts_models).")] string? defaultTtsModel = null,
        [Description("New default voice (for a model that supports voice selection).")] string? defaultVoice = null,
        [Description("New default BCP-47 language code.")] string? defaultLanguage = null,
        [Description("New tone instruction for gpt-oss preprocessing.")] string? tone = null,
        CancellationToken cancellationToken = default)
    {
        return _collectionService.UpdateAsync(id, new UpdateAudioCollectionRequestV1
        {
            Name = name,
            Description = description,
            DefaultTtsModel = defaultTtsModel,
            DefaultVoice = defaultVoice,
            DefaultLanguage = defaultLanguage,
            Tone = tone,
        }, cancellationToken);
    }

    [McpServerTool(Name = "delete_audio_collection", Title = "Delete Audio Collection")]
    [Description("Delete a channel. Recordings in the channel become unfiled (not cascade-deleted).")]
    public Task<bool> DeleteAudioCollection(
        [Description("Channel id.")] Guid id,
        CancellationToken cancellationToken = default)
    {
        return _collectionService.DeleteAsync(id, cancellationToken);
    }
}
