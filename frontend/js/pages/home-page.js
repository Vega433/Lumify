import { usersApi, videosApi } from "../api.js";
import { el } from "../dom.js";
import { createEmpty } from "../components/empty.js";
import { createSection } from "../components/section.js";
import { createVideoGrid } from "../components/video-grid.js";

function sameList(left, right) {
    if (left.length !== right.length) {
        return false;
    }

    return left.every((video, index) => video.videoId === right[index].videoId);
}

export function mountHomePage() {
    const root = el("div", "stack stack--lg");
    root.append(createEmpty("Загрузка…"));
    let alive = true;

    async function load() {
        try {
            const [feedResult, usersResult] = await Promise.allSettled([
                videosApi.feed(48),
                usersApi.list()
            ]);

            if (!alive) {
                return;
            }

            if (feedResult.status === "rejected") {
                throw feedResult.reason;
            }

            const names = new Map();
            if (usersResult.status === "fulfilled" && Array.isArray(usersResult.value)) {
                for (const user of usersResult.value) {
                    names.set(user.userId, user.userName);
                }
            }

            const recent = feedResult.value.recent ?? [];
            const recommended = feedResult.value.recommended ?? [];
            root.replaceChildren();

            if (!recent.length && !recommended.length) {
                root.append(
                    el("h1", "title", "Видео"),
                    createEmpty("Пока нет готовых роликов. Загрузи первое на своей странице.")
                );
                return;
            }

            if (sameList(recent, recommended)) {
                root.append(createSection("Видео", createVideoGrid(recent, names)));
                return;
            }

            root.append(
                createSection("Рекомендации", createVideoGrid(recommended, names)),
                createSection("Недавние", createVideoGrid(recent, names))
            );
        } catch (error) {
            if (!alive) {
                return;
            }

            root.replaceChildren(createEmpty(error.message));
        }
    }

    load();
    return {
        element: root,
        destroy() {
            alive = false;
        }
    };
}
