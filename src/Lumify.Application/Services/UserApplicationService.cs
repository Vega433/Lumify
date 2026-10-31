using Lumify.Application.DTOs;
using Lumify.Application.Extensions;
using Lumify.Application.Interfaces;
using Lumify.Application.Models;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Lumify.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumify.Application.Services;

public class UserApplicationService
{
    private static readonly HashSet<string> AllowedAvatarExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IFileStorageService _fileStorageService;
    private readonly ILumifyDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public UserApplicationService(
        IFileStorageService fileStorageService,
        ILumifyDbContext context,
        IPasswordHasher passwordHasher)
    {
        _fileStorageService = fileStorageService;
        _context = context;
        _passwordHasher = passwordHasher;
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

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        if (request.UserName == null
            && request.Email == null
            && request.BirthDate == null
            && request.NewPassword == null
            && request.CurrentPassword == null)
        {
            throw new ArgumentException("Nothing to update");
        }

        User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (request.UserName != null)
        {
            string userName = request.UserName.Trim();
            bool userNameBusy = await _context.Users
                .AnyAsync(u => u.UserId != userId && u.UserName == userName, ct);
            if (userNameBusy)
            {
                throw new InvalidOperationException($"A user with this username: {userName} is already registered.");
            }

            user.ChangeUserName(userName);
        }

        if (request.Email != null)
        {
            string email = Email.Create(request.Email).Value;
            bool emailBusy = await _context.Users
                .AnyAsync(u => u.UserId != userId && u.Email == email, ct);
            if (emailBusy)
            {
                throw new InvalidOperationException($"A user with this email: {email} is already registered.");
            }

            user.ChangeEmail(email);
        }

        if (request.BirthDate != null)
        {
            user.ChangeBirthDate(request.BirthDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (request.NewPassword != null || request.CurrentPassword != null)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new ArgumentException("Current password and new password are required together");
            }

            if (request.NewPassword.Length < 6)
            {
                throw new ArgumentException("Password must be at least 6 characters");
            }

            if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new InvalidOperationException("Incorrect password");
            }

            user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        }

        await _context.SaveChangesAsync(ct);
        return user.ToProfileDto();
    }

    public async Task<UserProfileDto> ChangeAvatarAsync(
        Guid userId,
        Stream avatar,
        string fileName,
        CancellationToken ct)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedAvatarExtensions.Contains(extension))
        {
            throw new InvalidDataException("Unsupported file extension");
        }

        User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        string? previousAvatar = user.AvatarPath;
        await _fileStorageService.SaveFileAsync(avatar, user.UserId, extension, Paths.Avatar, ct);

        string savedName = $"{user.UserId}{extension}";
        user.ChangeAvatar(savedName);
        await _context.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousAvatar)
            && !string.Equals(previousAvatar, savedName, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _fileStorageService.DeleteFile(previousAvatar, Paths.Avatar);
            }
            catch (IOException)
            {
            }
        }

        return user.ToProfileDto();
    }

    public async Task<UserProfileDto?> RemoveAvatarAsync(Guid userId, CancellationToken ct)
    {
        User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user == null)
        {
            return null;
        }

        string? previousAvatar = user.AvatarPath;
        user.RemoveAvatar();
        await _context.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousAvatar))
        {
            try
            {
                _fileStorageService.DeleteFile(previousAvatar, Paths.Avatar);
            }
            catch (IOException)
            {
            }
        }

        return user.ToProfileDto();
    }
}
