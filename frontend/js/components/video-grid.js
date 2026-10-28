import { el } from "../dom.js";
import { createEmpty } from "./empty.js";
import { createVideoCard } from "./video-card.js";

export function createVideoGrid(videos, names = new Map(), { onDelete, emptyText = "Пока нет видео" } = {}) {
    const list = Array.isArray(videos) ? videos : [];
    if (!list.length) {
        return createEmpty(emptyText);
    }

    const grid = el("div", "grid");
    for (const video of list) {
        grid.append(createVideoCard(video, {
            authorName: names.get(video.authorId),
            onDelete
        }));
    }

    return grid;
}
