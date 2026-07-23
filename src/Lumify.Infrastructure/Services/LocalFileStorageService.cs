using Lumify.Application.Interfaces;
using Lumify.Domain.Enums;

namespace Lumify.Infrastructure.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _basePath;

        public LocalFileStorageService(string uploadsPath)
        {
            _basePath = uploadsPath;
            
            foreach (Paths p in Enum.GetValues(typeof(Paths)))
            {
                string path = ChoosePath(p);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            }
        }
        
        public async Task<string> SaveFileAsync(Stream stream, Guid id, string fileEx, Paths targetPath,
            CancellationToken ct)
        {
            string folder = ChoosePath(targetPath);
            string fullPath = Path.Combine(folder, $"{id}{fileEx}");

            await SaveToDiskAsync(stream, fullPath, ct);
            return fullPath;
        }

        public void DeleteFile(string fileNameOrPath, Paths targetPath)
        {
            if (string.IsNullOrEmpty(fileNameOrPath)) return;
            
            string fullPath = Path.IsPathRooted(fileNameOrPath) 
                ? fileNameOrPath 
                : Path.Combine(ChoosePath(targetPath), fileNameOrPath);

            if (File.Exists(fullPath)) File.Delete(fullPath);
        }


        private async Task SaveToDiskAsync(Stream stream, string path, CancellationToken ct)
        {
            try
            {
                using Stream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                    useAsync: true);
                await stream.CopyToAsync(fileStream, ct);
            }
            catch (IOException ex)
            {
                if (File.Exists(path)) File.Delete(path);
                throw new InvalidOperationException("Storage error", ex);
            }
        }

        private string ChoosePath(Paths path) => path switch
        {
            Paths.basePath => _basePath,
            Paths.tempPath => Path.Combine(_basePath, "temp"),
            Paths.processedPath => Path.Combine(_basePath, "processed"),
            Paths.previewPath => Path.Combine(_basePath, "previews"),
            Paths.avatarPath => Path.Combine(_basePath, "avatars"),
            _ => throw new ArgumentOutOfRangeException(nameof(path))
        };
    }
}
