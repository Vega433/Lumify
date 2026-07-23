using Lumify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumify.Infrastructure.Persistence.Configurations
{
    public class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.HasKey(c => c.CommentId);
            builder.Property(c => c.CommentId).IsRequired();
            builder.Property(c => c.VideoId).IsRequired();
            builder.Property(c => c.AuthorId).IsRequired();
            builder.Property(c => c.Text).IsRequired().HasMaxLength(2000);
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.LastModifiedAt);
            builder.Property(c => c.ParentCommentId);

            builder.HasMany(c => c.Replies)
                   .WithOne(c => c.ParentComment)
                   .HasForeignKey(c => c.ParentCommentId)
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }
}