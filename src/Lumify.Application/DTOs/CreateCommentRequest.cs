namespace Lumify.Application.DTOs;

public record CreateCommentRequest(string Text, Guid? ParentCommentId);
