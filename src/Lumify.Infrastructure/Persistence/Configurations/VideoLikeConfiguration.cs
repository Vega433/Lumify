using Lumify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumify.Infrastructure.Persistence.Configurations;

public class VideoLikeConfiguration : IEntityTypeConfiguration<VideoLike>
{
    public void Configure(EntityTypeBuilder<VideoLike> builder)
    {
        builder.HasKey(like => new { like.UserId, like.VideoId });
        builder.Property(like => like.CreatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Video>()
            .WithMany()
            .HasForeignKey(like => like.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
