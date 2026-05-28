using DonkeyWork.Recordings.Storage.Contracts.Services;
using DonkeyWork.Recordings.Storage.Core.Options;
using DonkeyWork.Recordings.Storage.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Storage.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddStorageApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.AddSingleton<IS3ClientWrapper, S3ClientWrapper>();
        services.AddScoped<IStorageService, StorageService>();

        return services;
    }
}
