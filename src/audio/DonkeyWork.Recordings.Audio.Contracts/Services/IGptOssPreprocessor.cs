namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IGptOssPreprocessor
{
    Task<IReadOnlyList<string>> PreprocessAsync(GptOssPreprocessRequest request, CancellationToken cancellationToken = default);
}

public sealed record GptOssPreprocessRequest(string RawText, string? ChannelTone, string Language);
