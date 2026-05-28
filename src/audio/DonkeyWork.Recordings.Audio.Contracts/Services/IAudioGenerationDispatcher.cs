using DonkeyWork.Recordings.Audio.Contracts.Messages;

namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IAudioGenerationDispatcher
{
    ValueTask DispatchAsync(GenerateAudioRecordingCommand command, CancellationToken cancellationToken = default);
}
