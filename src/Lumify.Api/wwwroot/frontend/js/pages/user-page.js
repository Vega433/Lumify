import { usersApi, videosApi } from "../api.js";
import { el } from "../dom.js";
import { createEmpty } from "../components/empty.js";
import { createProfileHead } from "../components/profile-head.js";
import { createSection } from "../components/section.js";
import { createVideoGrid } from "../components/video-grid.js";
import { isReady } from "../format.js";

export function mountUserPage(userId) {
    const root = el("div", "stack stack--lg");
    root.append(createEmpty("Загрузка…"));
    let alive = true;

    async function load() {
        try {
            const [user, videos] = await Promise.all([
                usersApi.byId(userId),
                videosApi.byAuthor(userId)
            ]);

            if (!alive) {
                return;
            }

            document.title = `${user.userName} · Lumify`;
            const ready = (Array.isArray(videos) ? videos : []).filter((video) => isReady(video.status));
            const names = new Map([[user.userId, user.userName]]);
            root.replaceChildren(
                createProfileHead(user),
                createSection("Видео", createVideoGrid(ready, names, {
                    emptyText: "У этого автора пока нет готовых видео"
                }))
            );
        } catch (error) {
            if (!alive) {
                return;
            }

            const message = error.status === 404 ? "Пользователь не найден" : error.message;
            root.replaceChildren(createEmpty(message));
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
