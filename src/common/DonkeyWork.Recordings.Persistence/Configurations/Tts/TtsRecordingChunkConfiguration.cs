using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DonkeyWork.Recordings.Persistence.Configurations.Tts;

public class TtsRecordingChunkConfiguration : IEntityTypeConfiguration<TtsRecordingChunkEntity>
{
    public void Configure(EntityTypeBuilder<TtsRecordingChunkEntity> builder)
    {
        builder.ToTable("recording_chunks", "recordings");

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

        builder.Property(e => e.Index)
            .HasColumnName("chunk_index")
            .IsRequired();

        builder.Property(e => e.StoragePath)
            .HasColumnName("storage_path")
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.FilePath)
            .HasColumnName("file_path")
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(e => e.DurationSeconds)
            .HasColumnName("duration_seconds")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.RecordingId, e.Index }).IsUnique();

        builder.HasOne(e => e.Recording)
            .WithMany(r => r.Chunks)
            .HasForeignKey(e => e.RecordingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
