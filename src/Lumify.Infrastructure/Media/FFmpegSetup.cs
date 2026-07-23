using FFMpegCore;
using Microsoft.Extensions.Configuration;

namespace Lumify.Infrastructure.Media
{
    public static class FFmpegSetup
    {
        // По умолчанию ffmpeg/ffprobe ищутся в PATH.
        // Если BinaryFolder задан в конфиге — используем его.
        public static void ConfigureFrom(IConfiguration configuration)
        {
            string? folder = configuration["FFmpeg:BinaryFolder"];
            if (string.IsNullOrWhiteSpace(folder))
                return;

            GlobalFFOptions.Configure(options => options.BinaryFolder = folder);
        }
    }
}
