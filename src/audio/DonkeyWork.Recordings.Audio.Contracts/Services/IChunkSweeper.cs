namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IChunkSweeper
{
    // Deletes chunk clips (storage objects + DB rows) belonging to recordings that settled
    // (Ready/Failed) longer than the configured grace period ago. Returns the number of chunk
    // rows removed.
    Task<int> SweepOnceAsync(CancellationToken cancellationToken = default);
}
