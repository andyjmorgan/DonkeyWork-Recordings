using System.Threading.Channels;
using DonkeyWork.Recordings.Audio.Contracts.Messages;
using DonkeyWork.Recordings.Audio.Contracts.Services;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class InMemoryAudioGenerationDispatcher : IAudioGenerationDispatcher
{
    private readonly Channel<GenerateAudioRecordingCommand> _channel;

    public InMemoryAudioGenerationDispatcher(Channel<GenerateAudioRecordingCommand> channel)
    {
        _channel = channel;
    }

    public ValueTask DispatchAsync(GenerateAudioRecordingCommand command, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(command, cancellationToken);
}
