using Lumify.Application.DTOs;
using Lumify.Application.Interfaces;
using Lumify.Application.Extensions;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Lumify.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumify.Application.Services
{
    public class IdentityApplicationService
    {
        private readonly ILumifyDbContext _context;
        private readonly IPasswordHasher _hasher;
        private readonly IFileStorageService _fileStorageService;
        private readonly ITokenService _tokenService;

        public IdentityApplicationService(
            ILumifyDbContext context,
            IPasswordHasher hasher,
            IFileStorageService fileStorageService,
            ITokenService tokenService)
        {
            _context = context;
            _hasher = hasher;
            _fileStorageService = fileStorageService;
            _tokenService = tokenService;
        }

        public async Task<UserDto> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters");

            string email = Email.Create(request.Email).Value;

            bool emailBusy = await _context.Users.AnyAsync(u => u.Email == email, ct);
            if (emailBusy)
                throw new InvalidOperationException($"A user with this email: {email} is already registered.");

            string? savedAvatarName = null;

            using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                string passwordHash = _hasher.Hash(request.Password);

                User user = new User(
                    request.UserName.Trim(),
                    email,
                    request.BirthDate,
                    passwordHash,
                    avatarPath: null
                );

                if (request.Avatar != null && !string.IsNullOrEmpty(request.AvatarPath))
                {
                    string? extension = Path.GetExtension(request.AvatarPath)?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(extension) || !ValidateAvatarExtension(extension))
                        throw new InvalidDataException("Unsupported file extension");

                    await _fileStorageService.SaveFileAsync(
                        request.Avatar,
                        user.UserId,
                        extension,
                        Paths.avatarPath,
                        ct);

                    savedAvatarName = $"{user.UserId}{extension}";
                    user.ChangeAvatar(savedAvatarName);
                }

                _context.Users.Add(user);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return user.ToDto();
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                if (savedAvatarName != null)
                {
                    try { _fileStorageService.DeleteFile(savedAvatarName, Paths.avatarPath); }
                    catch { }
                }
                throw;
            }
        }

        public async Task<AuthResponse> AuthUserAsync(AuthUserRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                throw new InvalidOperationException("Incorrect email or password");

            string email;
            try
            {
                email = Email.Create(request.Email).Value;
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("Incorrect email or password");
            }

            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user == null || !_hasher.Verify(request.Password, user.PasswordHash))
                throw new InvalidOperationException("Incorrect email or password");

            string accessToken = _tokenService.GenerateAccessToken(user.ToAuthDto());
            string refreshToken = _tokenService.GenerateRefreshToken();

            user.UpdateToken(refreshToken);
            await _context.SaveChangesAsync(ct);

            return user.ToAuthResponse(accessToken, refreshToken);
        }

        public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidOperationException("Invalid refresh token");

            User? user = await _context.Users
                .FirstOrDefaultAsync(u => u.RefreshToken != null && u.RefreshToken.Token == refreshToken, ct);

            if (user?.RefreshToken == null || user.RefreshToken.IsExpired)
                throw new InvalidOperationException("Invalid or expired refresh token");

            string accessToken = _tokenService.GenerateAccessToken(user.ToAuthDto());
            string newRefreshToken = _tokenService.GenerateRefreshToken();

            user.UpdateToken(newRefreshToken);
            await _context.SaveChangesAsync(ct);

            return user.ToAuthResponse(accessToken, newRefreshToken);
        }

        public async Task LogoutAsync(Guid userId, CancellationToken ct)
        {
            User? user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user == null)
                return;

            user.ClearRefreshToken();
            await _context.SaveChangesAsync(ct);
        }

        private static bool ValidateAvatarExtension(string ex) =>
            new[] { ".jpg", ".png", ".jpeg", ".webp" }.Contains(ex);
    }
}
