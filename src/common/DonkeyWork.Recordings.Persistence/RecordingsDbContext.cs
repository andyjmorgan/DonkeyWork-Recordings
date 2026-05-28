using System.Reflection;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence.Entities;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DonkeyWork.Recordings.Persistence;

public class RecordingsDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly IIdentityContext? _identityContext;

    public RecordingsDbContext(DbContextOptions<RecordingsDbContext> options, IIdentityContext? identityContext = null)
        : base(options)
    {
        _identityContext = identityContext;
    }

    public Guid CurrentUserId => _identityContext?.UserId ?? Guid.Empty;

    public DbSet<TtsRecordingEntity> Recordings => Set<TtsRecordingEntity>();

    public DbSet<TtsAudioCollectionEntity> Collections => Set<TtsAudioCollectionEntity>();

    public DbSet<TtsPlaybackEntity> Playback => Set<TtsPlaybackEntity>();

    public DbSet<UserFeedSettingsEntity> UserFeedSettings => Set<UserFeedSettingsEntity>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecordingsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(RecordingsDbContext)
                    .GetMethod(nameof(ApplyUserFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyUserFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.UserId == CurrentUserId);
    }
}
