using Lumify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;


namespace Lumify.Application.Interfaces
{
    public interface ILumifyDbContext
    {
        DbSet<User> Users { get; }
        DbSet<Video> Videos { get; }
        DbSet<Comment> Comments { get; }

        DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
