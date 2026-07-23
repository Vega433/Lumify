using Lumify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumify.Infrastructure.Persistence.Configurations
{
    public class VideoConfiguration : IEntityTypeConfiguration<Video>
    {
        public void Configure(EntityTypeBuilder<Video> builder)
        {
            builder.HasKey(v => v.VideoId);
            builder.Property(v => v.VideoId).IsRequired();
            builder.Property(v => v.AuthorId).IsRequired();
            builder.Property(v => v.VideoFileName).IsRequired();
            builder.Property(v => v.PreviewFileName);
            builder.Property(v => v.Title).IsRequired().HasMaxLength(200);
            builder.Property(v => v.Description).HasMaxLength(1000);
            builder.Property(v => v.UploadAt).IsRequired();
            builder.Property(v => v.Views).IsRequired();
            builder.Property(v => v.IsSharing).IsRequired();
            builder.Property(v => v.Likes).IsRequired();
            builder.Property(v => v.Duration).HasConversion(
                v => v.Ticks,
                v => TimeSpan.FromTicks(v))
                .IsRequired();

            builder.HasMany(v => v.Comments)
                   .WithOne(c => c.Video)
                   .HasForeignKey(c => c.VideoId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.User)
                .WithMany(u => u.Videos)
                .HasForeignKey(v => v.AuthorId);
        }
    }
}
