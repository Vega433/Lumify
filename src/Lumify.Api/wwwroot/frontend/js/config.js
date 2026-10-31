// Адрес API. Если сайт открыт с самого API (/app на порту 5247 или 7246), запросы идут туда же.
const apiPort = window.location.port;
const servedByApi = apiPort === "5247" || apiPort === "7246";

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
