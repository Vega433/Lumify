using Microsoft.EntityFrameworkCore;
using Lumify.Domain.Entities;
using Lumify.Application.Interfaces;

namespace Lumify.Infrastructure.Persistence
{
    public class AppDbContext : DbContext, ILumifyDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Video> Videos => Set<Video>();
        public DbSet<Comment> Comments => Set<Comment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
