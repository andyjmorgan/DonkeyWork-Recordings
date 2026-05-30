namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface ITtsProviderRegistry
{
    IReadOnlyList<ITtsProvider> Providers { get; }

    ITtsProvider Default { get; }

    ITtsProvider Resolve(string? key);
}
