using Lumify.Domain.Entities;
using Lumify.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumify.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.UserId);

            builder.Property(u => u.UserName).IsRequired().HasMaxLength(100);
            builder.HasIndex(u => u.UserName).IsUnique();

            builder.Property(u => u.PasswordHash).IsRequired();

            builder.Property(u => u.Email)
                .HasMaxLength(256)
                .IsRequired();
            builder.HasIndex(u => u.Email).IsUnique();

            builder.Property(u => u.AvatarPath);

            builder.Property(u => u.BirthDate)
                .HasConversion(
                    v => DateOnly.FromDateTime(v.Value),
                    v => BirthDate.FromDatabase(v))
                .HasColumnType("date")
                .IsRequired();

            builder.OwnsOne(u => u.RefreshToken, nav =>
            {
                nav.Property(t => t.Token).HasColumnName("refresh_token");
                nav.Property(t => t.CreatedAt).HasColumnName("refresh_token_created_at");
                nav.Property(t => t.ExpiresAt).HasColumnName("refresh_token_expires_at");
            });
            builder.Navigation(u => u.RefreshToken).IsRequired(false);

            builder.Property(u => u.CreatedAt).IsRequired();

            builder.HasMany(u => u.Comments)
                .WithOne(c => c.Author)
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(u => u.Videos)
                .WithOne(v => v.User)
                .HasForeignKey(v => v.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
