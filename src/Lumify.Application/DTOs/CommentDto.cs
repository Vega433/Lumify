namespace Lumify.Application.DTOs;

public record CommentDto(
    Guid CommentId,
    Guid VideoId,
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Text,
    DateTime CreatedAt,
    DateTime? LastModifiedAt,
    Guid? ParentCommentId
    );
