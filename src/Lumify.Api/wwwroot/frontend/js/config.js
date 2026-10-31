// Сайт открыт с самого API (/app). Запросы идут на тот же адрес и порт,
// с которого загрузилась страница: 5247 у dotnet run, 8080 у контейнера.
// file:// — страница открыта с диска, тогда API ищется на localhost:5247.
const servedByApi = window.location.protocol === "http:" || window.location.protocol === "https:";

export const API_ORIGIN = servedByApi ? window.location.origin : "http://localhost:5247";

export function mediaUrl(path) {
    if (!path) {
        return "";
    }

    if (path.startsWith("http://") || path.startsWith("https://")) {
        return path;
    }

    return `${API_ORIGIN}${path.startsWith("/") ? path : `/${path}`}`;
}

// В DTO сейчас часто лежит /uploads/processed/{id}.mp4, а worker пишет HLS в папку {id}/index.m3u8.
export function playbackUrl(video) {
    const given = video.videoUrl || "";
    if (given.endsWith(".m3u8")) {
        return mediaUrl(given);
    }

    return mediaUrl(`/uploads/processed/${video.videoId}/index.m3u8`);
}
