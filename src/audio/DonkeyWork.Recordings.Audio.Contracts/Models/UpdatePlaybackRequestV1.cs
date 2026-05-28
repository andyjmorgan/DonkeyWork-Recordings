namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class UpdatePlaybackRequestV1
{
    public required double PositionSeconds { get; init; }

    public double? DurationSeconds { get; init; }

    public bool? Completed { get; init; }

    public double? PlaybackSpeed { get; init; }
}
