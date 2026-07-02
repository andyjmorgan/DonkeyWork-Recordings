using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DonkeyWork.Recordings.Persistence.Configurations.Tts;

public class TtsBacklogItemConfiguration : IEntityTypeConfiguration<TtsBacklogItemEntity>
{
    public void Configure(EntityTypeBuilder<TtsBacklogItemEntity> builder)
    {
        builder.ToTable("backlog_items", "recordings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.CollectionId)
            .HasColumnName("collection_id")
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(e => e.SourceUrl)
            .HasColumnName("source_url")
            .HasMaxLength(1000);

        builder.Property(e => e.Notes)
            .HasColumnName("notes");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ConsumedAt)
            .HasColumnName("consumed_at");

        builder.Property(e => e.ConsumedByRecordingId)
            .HasColumnName("consumed_by_recording_id");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.CollectionId, e.Status });
        builder.HasIndex(e => e.ConsumedByRecordingId);

        builder.HasOne(e => e.Collection)
            .WithMany(c => c.BacklogItems)
            .HasForeignKey(e => e.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ConsumedByRecording)
            .WithMany()
            .HasForeignKey(e => e.ConsumedByRecordingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
