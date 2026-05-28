using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DonkeyWork.Recordings.Persistence.Configurations.Tts;

public class TtsPlaybackConfiguration : IEntityTypeConfiguration<TtsPlaybackEntity>
{
    public void Configure(EntityTypeBuilder<TtsPlaybackEntity> builder)
    {
        builder.ToTable("playback", "recordings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.RecordingId)
            .HasColumnName("recording_id")
            .IsRequired();

        builder.Property(e => e.PositionSeconds)
            .HasColumnName("position_seconds")
            .IsRequired();

        builder.Property(e => e.DurationSeconds)
            .HasColumnName("duration_seconds")
            .IsRequired();

        builder.Property(e => e.Completed)
            .HasColumnName("completed")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.PlaybackSpeed)
            .HasColumnName("playback_speed")
            .IsRequired()
            .HasDefaultValue(1.0);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => new { e.UserId, e.RecordingId })
            .IsUnique();
    }
}
