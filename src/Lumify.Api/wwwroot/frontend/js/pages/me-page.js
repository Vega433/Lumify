import { usersApi, videosApi } from "../api.js";
import { el } from "../dom.js";
import { navigate, rememberNext } from "../navigation.js";
import { createAlert } from "../components/alert.js";
import { createEmpty } from "../components/empty.js";
import { createProfileHead } from "../components/profile-head.js";
import { createProfileSettings } from "../components/profile-settings.js";
import { createSection } from "../components/section.js";
import { createUploadForm } from "../components/upload-form.js";
import { createVideoGrid } from "../components/video-grid.js";
import { isProcessing } from "../format.js";

export function mountMePage() {
    const root = el("div", "stack stack--lg");
    const profileSlot = el("div", "tile tile--hero me-grid__wide");
    const listSlot = el("div", "me-grid__wide");
    const alert = createAlert();
    const upload = createUploadForm(() => refresh());

    let alive = true;
    let built = false;
    let timer = null;
    let generation = 0;

    function schedule(videos) {
        const waiting = videos.some((video) => isProcessing(video.status));
        if (waiting && !timer) {
            timer = setInterval(() => {
                refresh();
            }, 5000);
            return;
        }

        if (!waiting && timer) {
            clearInterval(timer);
            timer = null;
        }
    }

    function refresh() {
        generation += 1;
        return load(generation);
    }

    function showProfile(profile) {
        profileSlot.replaceChildren(createProfileHead(profile, { showPrivate: true, hero: true }));
    }

    async function load(ticket) {
        try {
            const profile = await usersApi.me();
            const videos = await videosApi.mine();
            if (!alive || ticket !== generation) {
                return;
            }

            const list = Array.isArray(videos) ? videos : [];
            showProfile(profile);
            listSlot.replaceChildren(createSection("Мои видео", createVideoGrid(
                list,
                new Map([[profile.userId, profile.userName]]),
                {
                    emptyText: "Ты ещё ничего не загрузил",
                    onDelete: async (video) => {
                        await videosApi.remove(video.videoId);
                        await refresh();
                    }
                }
            )));

            if (!built) {
                const grid = el("div", "me-grid");
                grid.append(profileSlot, createProfileSettings(profile, showProfile), upload, listSlot);
                root.replaceChildren(alert.element, grid);
                built = true;
            }

            alert.hide();
            schedule(list);
        } catch (error) {
            if (!alive || ticket !== generation) {
                return;
            }

            if (error.status === 401) {
                rememberNext("#/me");
                navigate("#/login");
                return;
            }

            if (!built) {
                root.replaceChildren(createEmpty(error.message));
                return;
            }

            alert.show(error.message);
        }
    }

    root.append(createEmpty("Загрузка…"));
    refresh();

    return {
        element: root,
        destroy() {
            alive = false;
            if (timer) {
                clearInterval(timer);
            }
        }
    };
}
