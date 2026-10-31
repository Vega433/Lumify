import { usersApi, videosApi } from "../api.js";
import { mediaUrl, playbackUrl } from "../config.js";
import { el } from "../dom.js";
import { createAvatar } from "../components/avatar.js";
import { createEmpty } from "../components/empty.js";
import { createStatusBadge } from "../components/status-badge.js";
import { createPlayer } from "../components/player.js";
import { formatDate, formatDuration, formatViews, isFailed, isProcessing, isReady } from "../format.js";

export function mountVideoPage(videoId) {
    const root = el("div", "stack");
    root.append(createEmpty("Загрузка…"));

    let alive = true;
    let timer = null;
    let player = null;

    function show(video, author) {
        player?.destroy();
        player = null;
        document.title = `${video.title} · Lumify`;

        const blocks = [];
        if (isReady(video.status)) {
            player = createPlayer(playbackUrl(video), video.previewUrl ? mediaUrl(video.previewUrl) : "");
            blocks.push(player.element);
        } else if (isFailed(video.status)) {
            blocks.push(createEmpty("Обработка не удалась. Это видео нельзя воспроизвести."));
        } else {
            blocks.push(createEmpty("Видео ещё обрабатывается. Страница обновится сама."));
        }

        blocks.push(el("h1", "title", video.title));

        const meta = el("div", "meta");
        const badge = createStatusBadge(video.status);
        if (badge) {
            meta.append(badge);
        }

        meta.append(
            el("span", null, formatViews(video.views)),
            el("span", null, formatDate(video.uploadAt)),
            el("span", null, formatDuration(video.duration))
        );
        blocks.push(meta);

        if (video.description) {
            blocks.push(el("p", "lead", video.description));
        }

        const authorLink = document.createElement("a");
        authorLink.className = "author-line";
        authorLink.href = `#/user/${video.authorId}`;
        authorLink.append(
            createAvatar(author || { userName: author?.userName || "Автор" }, "sm"),
            el("span", null, author?.userName || "Автор")
        );
        blocks.push(authorLink);
        root.replaceChildren(...blocks);
    }

    async function load() {
        try {
            const video = await videosApi.byId(videoId);
            if (!alive) {
                return;
            }

            let author = null;
            try {
                author = await usersApi.byId(video.authorId);
            } catch {
                author = null;
            }

            if (!alive) {
                return;
            }

            show(video, author);

            if (isProcessing(video.status)) {
                if (!timer) {
                    timer = setInterval(load, 4000);
                }
            } else if (timer) {
                clearInterval(timer);
                timer = null;
            }
        } catch (error) {
            if (!alive) {
                return;
            }

            if (timer) {
                clearInterval(timer);
                timer = null;
            }

            const message = error.status === 404 ? "Видео не найдено" : error.message;
            root.replaceChildren(createEmpty(message));
        }
    }

    load();
    return {
        element: root,
        destroy() {
            alive = false;
            if (timer) {
                clearInterval(timer);
            }

            player?.destroy();
        }
    };
}
