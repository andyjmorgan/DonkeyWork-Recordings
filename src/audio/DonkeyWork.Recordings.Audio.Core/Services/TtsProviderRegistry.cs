using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class TtsProviderRegistry : ITtsProviderRegistry
{
    private readonly Dictionary<string, ITtsProvider> _byKey;

    public TtsProviderRegistry(IEnumerable<ITtsProvider> providers, IOptions<TtsOptions> options)
    {
        Providers = providers.ToList();
        _byKey = Providers.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        Default = _byKey.TryGetValue(options.Value.DefaultModel, out var configured)
            ? configured
            : Providers[0];
    }

    public IReadOnlyList<ITtsProvider> Providers { get; }

    public ITtsProvider Default { get; }

    public ITtsProvider Resolve(string? key) =>
        !string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key, out var provider)
            ? provider
            : Default;
}
