using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DonkeyWork.Recordings.Persistence.Configurations.Tts;

public class TtsAudioCollectionConfiguration : IEntityTypeConfiguration<TtsAudioCollectionEntity>
{
    public void Configure(EntityTypeBuilder<TtsAudioCollectionEntity> builder)
    {
        builder.ToTable("collections", "recordings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(e => e.CoverImagePath)
            .HasColumnName("cover_image_path")
            .HasMaxLength(1000);

        builder.Property(e => e.DefaultTtsModel)
            .HasColumnName("default_tts_model")
            .HasMaxLength(50);

        builder.Property(e => e.DefaultVoice)
            .HasColumnName("default_voice")
            .HasMaxLength(200);

        builder.Property(e => e.DefaultLanguage)
            .HasColumnName("default_language")
            .HasMaxLength(20);

        builder.Property(e => e.Tone)
            .HasColumnName("tone");

        builder.Property(e => e.Author)
            .HasColumnName("author")
            .HasMaxLength(200);

        builder.Property(e => e.AuthorEmail)
            .HasColumnName("author_email")
            .HasMaxLength(200);

        builder.Property(e => e.ItunesCategory)
            .HasColumnName("itunes_category")
            .HasMaxLength(100);

        builder.Property(e => e.IsExplicit)
            .HasColumnName("is_explicit")
            .IsRequired();

        builder.Property(e => e.Link)
            .HasColumnName("link")
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => e.UserId);

        builder.HasMany(e => e.Recordings)
            .WithOne(r => r.Collection)
            .HasForeignKey(r => r.CollectionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
