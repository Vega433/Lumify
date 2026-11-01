import { usersApi, videosApi } from "../api.js";
import { mediaUrl, playbackUrl } from "../config.js";
import { el, setBusy } from "../dom.js";
import { formatCount, formatDate, formatDuration, isFailed, isProcessing, isReady, sameId } from "../format.js";
import { navigate, rememberNext } from "../navigation.js";
import { getSession } from "../session.js";
import { createAlert } from "../components/alert.js";
import { createAvatar } from "../components/avatar.js";
import { createComments } from "../components/comments.js";
import { createEmpty } from "../components/empty.js";
import { createField } from "../components/field.js";
import { iconHeart } from "../components/icons.js";
import { createPlayer } from "../components/player.js";
import { createStatusBadge } from "../components/status-badge.js";

export function mountVideoPage(videoId) {
    const root = el("div", "stack stack--lg");
    const stage = el("div");
    const statsRow = el("div", "bento bento--stats");
    const info = el("section", "tile stack");
    const alert = createAlert();
    let laidOut = false;
    root.append(createEmpty("Загрузка…"));

    let alive = true;
    let timer = null;
    let player = null;
    let comments = null;
    let viewed = false;
    let editing = false;
    let likedByMe = false;
    let current = null;
    let author = null;
    let shownKey = "";

    function stat(value, label) {
        const tile = el("div", "tile stat");
        tile.append(el("p", "stat__value", value), el("p", "stat__label", label));
        return tile;
    }

    function paintStats(video) {
        statsRow.replaceChildren(
            stat(formatCount(video.views), "просмотры"),
            stat(formatCount(video.likes), "лайки"),
            stat(formatDuration(video.duration), "длительность"),
            stat(formatDate(video.uploadAt), "загружено")
        );
    }

    function paintStage(video) {
        const key = `${video.status}`;
        if (key === shownKey && stage.childNodes.length) {
            return;
        }

        shownKey = key;
        player?.destroy();
        player = null;
        if (isReady(video.status)) {
            player = createPlayer(playbackUrl(video), video.previewUrl ? mediaUrl(video.previewUrl) : "");
            stage.replaceChildren(player.element);
            return;
        }

        if (isFailed(video.status)) {
            stage.replaceChildren(createEmpty("Обработка не удалась. Это видео нельзя воспроизвести."));
            return;
        }

        stage.replaceChildren(createEmpty("Видео ещё обрабатывается. Страница обновится сама."));
    }

    function paintInfo(video) {
        if (editing) {
            return;
        }

        info.replaceChildren();
        const titleRow = el("div", "title-row");
        titleRow.append(el("h1", "title", video.title));
        const badge = createStatusBadge(video.status);
        if (badge) {
            titleRow.append(badge);
        }

        info.append(titleRow);

        const authorLink = document.createElement("a");
        authorLink.className = "author-line";
        authorLink.href = `#/user/${video.authorId}`;
        authorLink.append(
            createAvatar(author || { userName: "Автор" }, "sm"),
            el("span", null, author?.userName || "Автор")
        );
        info.append(authorLink);

        info.append(el("p", "lead", video.description || "Без описания"));

        const actions = el("div", "actions");
        const like = document.createElement("button");
        like.type = "button";
        like.className = likedByMe ? "like-button is-liked" : "like-button";
        like.setAttribute("aria-label", likedByMe ? "Убрать отметку" : "Нравится");
        like.append(iconHeart(), el("span", "like-button__count", formatCount(video.likes)));
        like.addEventListener("click", async () => {
            if (!getSession()) {
                rememberNext(location.hash || `#/video/${videoId}`);
                navigate("#/login");
                return;
            }

            like.disabled = true;
            try {
                const stats = likedByMe
                    ? await videosApi.unlike(videoId)
                    : await videosApi.like(videoId);
                likedByMe = Boolean(stats.likedByMe);
                current = { ...current, views: stats.views, likes: stats.likes };
                paintStats(current);
                paintInfo(current);
            } catch (error) {
                alert.show(error.message);
                like.disabled = false;
            }
        });
        actions.append(like);

        const mine = sameId(getSession()?.user?.userId, video.authorId);
        if (mine) {
            const edit = document.createElement("button");
            edit.type = "button";
            edit.className = "button button--small button--ghost";
            edit.textContent = "Изменить";
            edit.addEventListener("click", () => openEditor(video));
            actions.append(edit);

            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "button button--small button--danger";
            remove.textContent = "Удалить видео";
            remove.addEventListener("click", async () => {
                if (!window.confirm(`Удалить «${video.title}»?`)) {
                    return;
                }

                setBusy(remove, true, "Удаляем…");
                try {
                    await videosApi.remove(video.videoId);
                    navigate("#/me");
                } catch (error) {
                    alert.show(error.message);
                    setBusy(remove, false);
                }
            });
            actions.append(remove);
        }

        info.append(actions);
    }

    function openEditor(video) {
        editing = true;
        info.replaceChildren(el("h2", "tile__title", "Название и описание"));
        const form = document.createElement("form");
        form.className = "form";
        form.append(
            createField({
                label: "Название",
                name: "title",
                required: true,
                maxLength: 200,
                value: video.title
            }),
            createField({
                label: "Описание",
                name: "description",
                type: "textarea",
                maxLength: 1000,
                value: video.description || ""
            })
        );
        const row = el("div", "actions");
        const save = document.createElement("button");
        save.className = "button button--small";
        save.type = "submit";
        save.textContent = "Сохранить";
        const cancel = document.createElement("button");
        cancel.type = "button";
        cancel.className = "button button--small button--ghost";
        cancel.textContent = "Отмена";
        cancel.addEventListener("click", () => {
            editing = false;
            paintInfo(current);
        });
        row.append(save, cancel);
        form.append(row);
        form.addEventListener("submit", async (event) => {
            event.preventDefault();
            alert.hide();
            setBusy(save, true, "Сохраняем…");
            try {
                const next = await videosApi.update(video.videoId, {
                    title: form.querySelector("[name=title]").value.trim(),
                    description: form.querySelector("[name=description]").value.trim()
                });
                current = { ...current, ...next, views: current.views, likes: current.likes };
                editing = false;
                document.title = `${current.title} · Lumify`;
                paintInfo(current);
                alert.show("Видео обновлено", "ok");
            } catch (error) {
                alert.show(error.message);
                setBusy(save, false);
            }
        });
        info.append(form);
    }

    function show(video) {
        document.title = `${video.title} · Lumify`;
        if (!laidOut) {
            root.replaceChildren(stage, alert.element, statsRow, info);
            laidOut = true;
        }

        paintStage(video);
        paintStats(video);
        paintInfo(video);
        if (!comments) {
            comments = createComments(videoId);
            root.append(comments.element);
        }
    }

    async function load() {
        try {
            const video = await videosApi.byId(videoId);
            if (!alive) {
                return;
            }

            if (!author) {
                try {
                    author = await usersApi.byId(video.authorId);
                } catch {
                    author = null;
                }
            }

            if (!alive) {
                return;
            }

            current = video;
            if (!viewed) {
                try {
                    const stats = await videosApi.addView(videoId);
                    if (!alive) {
                        return;
                    }

                    viewed = true;
                    likedByMe = Boolean(stats.likedByMe);
                    current = { ...video, views: stats.views, likes: stats.likes };
                } catch {
                    viewed = true;
                }
            }

            if (!alive) {
                return;
            }

            show(current);
            alert.hide();

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
            comments?.destroy();
            comments = null;
            player?.destroy();
            player = null;
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
            comments?.destroy();
        }
    };
}
