namespace Lumify.Application.DTOs
{
    public record UploadVideoRequest(
        string Title,
        string? Description,
        Guid AuthorId,
        Stream FileStream,
        string FileName,
        Stream? Preview = null,
        string? PreviewName = null
        );
}
