import { mediaUrl } from "../config.js";
import { el } from "../dom.js";
import { formatCount, hasDuration, formatDuration } from "../format.js";
import { iconEye, iconHeart } from "./icons.js";
import { createStatusBadge } from "./status-badge.js";

export function createVideoCard(video, { authorName, onDelete } = {}) {
    const card = el("article", "card");
    const href = `#/video/${video.videoId}`;

    const media = document.createElement("a");
    media.className = "card__media";
    media.href = href;
    if (video.previewUrl) {
        const img = document.createElement("img");
        img.src = mediaUrl(video.previewUrl);
        img.alt = "";
        media.append(img);
    } else {
        media.append(el("div", "card__placeholder"));
    }

    if (hasDuration(video.duration)) {
        media.append(el("span", "card__duration", formatDuration(video.duration)));
    }

    const body = el("div", "card__body");
    const title = document.createElement("a");
    title.className = "card__title";
    title.href = href;
    title.textContent = video.title;

    const author = document.createElement("a");
    author.className = "card__author";
    author.href = `#/user/${video.authorId}`;
    author.textContent = authorName || "Автор";

    const facts = el("p", "card__facts");
    const views = el("span", "fact");
    views.append(iconEye(), el("span", null, formatCount(video.views)));
    const likes = el("span", "fact");
    likes.append(iconHeart(), el("span", null, formatCount(video.likes)));
    facts.append(views, likes);
    body.append(title, author, facts);

    const badge = createStatusBadge(video.status, { hideReady: true });
    if (badge) {
        body.append(badge);
    }

    if (onDelete) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "button button--small button--danger";
        button.textContent = "Удалить";
        button.addEventListener("click", async () => {
            if (!window.confirm(`Удалить «${video.title}»?`)) {
                return;
            }

            button.disabled = true;
            try {
                await onDelete(video);
            } catch (error) {
                button.disabled = false;
                window.alert(error.message);
            }
        });
        body.append(button);
    }

    card.append(media, body);
    return card;
}
