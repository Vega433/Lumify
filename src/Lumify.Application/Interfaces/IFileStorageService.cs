using Lumify.Domain.Enums;

namespace Lumify.Application.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(Stream stream, Guid id, string fileEx, Paths targetPath, CancellationToken ct);
        void DeleteFile(string fileName, Paths targetPath);
    }

}
