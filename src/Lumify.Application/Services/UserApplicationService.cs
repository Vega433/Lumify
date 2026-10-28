using Lumify.Application.DTOs;
using Lumify.Application.Extensions;
using Lumify.Application.Interfaces;
using Lumify.Application.Models;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumify.Application.Services;

public class UserApplicationService
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ILumifyDbContext _context;

    public UserApplicationService(
        IFileStorageService fileStorageService,
        ILumifyDbContext context)
    {
        _fileStorageService = fileStorageService;
        _context = context;
    }

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct)
    {
        List<User> users = await _context.Users.ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct)
    {
        User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        return user?.ToDto();
    }

    public async Task<UserProfileDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        return user?.ToProfileDto();
    }

    public async Task<UserDeleteResult> DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken ct)
    {
        if (userId != currentUserId)
        {
            return UserDeleteResult.Forbidden;
        }

        using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(ct);
        string? avatarPath;

        try
        {
            User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user == null)
            {
                return UserDeleteResult.NotFound;
            }

            avatarPath = user.AvatarPath;
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            return UserDeleteResult.Failed;
        }

        if (!string.IsNullOrEmpty(avatarPath))
        {
            try
            {
                _fileStorageService.DeleteFile(avatarPath, Paths.Avatar);
            }
            catch (IOException)
            {
            }
        }

        return UserDeleteResult.Deleted;
    }
}
