using Lumify.Application.DTOs;
using Lumify.Application.Extensions;
using Lumify.Application.Interfaces;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumify.Application.Services
{
    public class UserApplicationService
    {
        private readonly IFileStorageService _storageService;
        private readonly ILumifyDbContext _dbContext;

        public UserApplicationService(
            IFileStorageService storageService,
            ILumifyDbContext dbContext)
        {
            _storageService = storageService;
            _dbContext = dbContext;
        }

        public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct)
        {
            List<User> users = await _dbContext.Users.ToListAsync(ct);
            return users.Select(u => u.ToDto()).ToList();
        }

        public async Task<bool> DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken ct)
        {
            if (userId != currentUserId) return false; 

            using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            string? avatarPath;

            try
            {
                User? user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
                if (user == null) return true; 

                avatarPath = user.AvatarPath;
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw; 
            }

            if (!string.IsNullOrEmpty(avatarPath))
            {
                try
                {
                    _storageService.DeleteFile(avatarPath, Paths.avatarPath);
                }
                catch (IOException) { }
            }

            return true;
        }

    }
}
