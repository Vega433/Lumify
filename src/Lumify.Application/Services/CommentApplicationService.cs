using Lumify.Application.DTOs;
using Lumify.Application.Interfaces;
using Lumify.Application.Models;
using Lumify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lumify.Application.Services;

public class CommentApplicationService
{
    private readonly ILumifyDbContext _context;

    public CommentApplicationService(ILumifyDbContext context)
    {
        _context = context;
    }

    public async Task<List<CommentDto>?> GetByVideoAsync(Guid videoId, CancellationToken ct)
    {
        bool videoExists = await _context.Videos.AnyAsync(v => v.VideoId == videoId, ct);
        if (!videoExists)
        {
            return null;
        }

        return await _context.Comments
            .Where(c => c.VideoId == videoId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.CommentId,
                c.VideoId,
                c.AuthorId,
                c.Author.UserName,
                c.Author.AvatarPath == null ? null : "/uploads/avatars/" + c.Author.AvatarPath,
                c.Text,
                c.CreatedAt,
                c.LastModifiedAt,
                c.ParentCommentId))
            .ToListAsync(ct);
    }

    public async Task<CommentDto> CreateAsync(
        Guid videoId,
        Guid authorId,
        CreateCommentRequest request,
        CancellationToken ct)
    {
        bool videoExists = await _context.Videos.AnyAsync(v => v.VideoId == videoId, ct);
        if (!videoExists)
        {
            throw new KeyNotFoundException("Video not found");
        }

        bool authorExists = await _context.Users.AnyAsync(u => u.UserId == authorId, ct);
        if (!authorExists)
        {
            throw new InvalidOperationException("Author not found");
        }

        if (request.ParentCommentId != null)
        {
            Comment? parent = await _context.Comments
                .FirstOrDefaultAsync(c => c.CommentId == request.ParentCommentId, ct);

            if (parent == null || parent.VideoId != videoId)
            {
                throw new ArgumentException("Parent comment was not found on this video");
            }

            if (parent.ParentCommentId != null)
            {
                throw new ArgumentException("Replies can only target a top-level comment");
            }
        }

        Comment comment = new Comment(authorId, videoId, request.Text.Trim(), request.ParentCommentId);
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync(ct);

        User author = await _context.Users.FirstAsync(u => u.UserId == authorId, ct);
        return ToDto(comment, author);
    }

    public async Task<(CommentAccess Access, CommentDto? Comment)> UpdateAsync(
        Guid commentId,
        Guid userId,
        string text,
        CancellationToken ct)
    {
        Comment? comment = await _context.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId, ct);
        if (comment == null)
        {
            return (CommentAccess.NotFound, null);
        }

        if (comment.AuthorId != userId)
        {
            return (CommentAccess.Forbidden, null);
        }

        comment.Update(text.Trim());
        await _context.SaveChangesAsync(ct);

        User author = await _context.Users.FirstAsync(u => u.UserId == userId, ct);
        return (CommentAccess.Ok, ToDto(comment, author));
    }

    public async Task<CommentAccess> DeleteAsync(Guid commentId, Guid userId, CancellationToken ct)
    {
        Comment? comment = await _context.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId, ct);
        if (comment == null)
        {
            return CommentAccess.NotFound;
        }

        if (comment.AuthorId != userId)
        {
            return CommentAccess.Forbidden;
        }

        List<Comment> replies = await _context.Comments
            .Where(c => c.ParentCommentId == commentId)
            .ToListAsync(ct);

        _context.Comments.RemoveRange(replies);
        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync(ct);
        return CommentAccess.Ok;
    }

    private static CommentDto ToDto(Comment comment, User author) => new CommentDto(
        comment.CommentId,
        comment.VideoId,
        comment.AuthorId,
        author.UserName,
        string.IsNullOrEmpty(author.AvatarPath) ? null : $"/uploads/avatars/{author.AvatarPath}",
        comment.Text,
        comment.CreatedAt,
        comment.LastModifiedAt,
        comment.ParentCommentId
        );
}
