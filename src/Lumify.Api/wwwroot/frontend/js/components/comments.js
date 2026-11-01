import { commentsApi } from "../api.js";
import { el, setBusy } from "../dom.js";
import { formatDate, sameId } from "../format.js";
import { rememberNext } from "../navigation.js";
import { getSession } from "../session.js";
import { createAlert } from "./alert.js";
import { createAvatar } from "./avatar.js";
import { createEmpty } from "./empty.js";

function textButton(label, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "text-btn";
    button.textContent = label;
    button.addEventListener("click", onClick);
    return button;
}

export function createComments(videoId) {
    const root = el("section", "tile stack");
    const list = el("div", "stack");
    const alert = createAlert();
    let alive = true;
    let replyTo = null;
    let editingId = null;
    let cache = [];

    root.append(el("h2", "tile__title", "Комментарии"), alert.element, list);

    function composer(parentCommentId) {
        const session = getSession();
        if (!session) {
            const note = el("p", "hint");
            const link = document.createElement("a");
            link.className = "text-link";
            link.href = "#/login";
            link.textContent = "Войди";
            link.addEventListener("click", () => rememberNext(location.hash || "#/"));
            const tail = parentCommentId ? ", чтобы ответить." : ", чтобы оставить комментарий.";
            note.append(link, document.createTextNode(tail));
            return note;
        }

        const form = document.createElement("form");
        form.className = "form";
        const input = document.createElement("textarea");
        input.className = "field__input";
        input.required = true;
        input.maxLength = 2000;
        input.rows = parentCommentId ? 2 : 3;
        input.placeholder = parentCommentId ? "Ответ" : "Комментарий";
        const button = document.createElement("button");
        button.className = "button button--small";
        button.type = "submit";
        button.textContent = parentCommentId ? "Ответить" : "Отправить";
        form.append(input, button);

        form.addEventListener("submit", async (event) => {
            event.preventDefault();
            alert.hide();
            const text = input.value.trim();
            if (!text) {
                return;
            }

            setBusy(button, true, "Отправляем…");
            try {
                await commentsApi.create(videoId, text, parentCommentId);
                replyTo = null;
                await load();
            } catch (error) {
                alert.show(error.message);
                setBusy(button, false);
            }
        });

        return form;
    }

    function editForm(comment) {
        const form = document.createElement("form");
        form.className = "form";
        const input = document.createElement("textarea");
        input.className = "field__input";
        input.required = true;
        input.maxLength = 2000;
        input.rows = 3;
        input.value = comment.text;
        const row = el("div", "actions");
        const save = document.createElement("button");
        save.className = "button button--small";
        save.type = "submit";
        save.textContent = "Сохранить";
        row.append(save, textButton("Отмена", () => {
            editingId = null;
            paint();
        }));
        form.append(input, row);
        form.addEventListener("submit", async (event) => {
            event.preventDefault();
            alert.hide();
            setBusy(save, true, "Сохраняем…");
            try {
                await commentsApi.update(comment.commentId, input.value.trim());
                editingId = null;
                await load();
            } catch (error) {
                alert.show(error.message);
                setBusy(save, false);
            }
        });
        return form;
    }

    async function removeComment(comment) {
        const question = comment.parentCommentId
            ? "Удалить ответ?"
            : "Удалить комментарий? Ответы на него тоже пропадут.";
        if (!window.confirm(question)) {
            return;
        }

        alert.hide();
        try {
            await commentsApi.remove(comment.commentId);
            if (sameId(replyTo, comment.commentId)) {
                replyTo = null;
            }

            await load();
        } catch (error) {
            alert.show(error.message);
        }
    }

    function commentHead(comment) {
        const head = el("div", "comment__head");
        const who = document.createElement("a");
        who.href = `#/user/${comment.authorId}`;
        who.append(createAvatar({
            userName: comment.authorName,
            avatarUrl: comment.authorAvatarUrl
        }, "sm"));
        const name = document.createElement("a");
        name.className = "comment__name";
        name.href = `#/user/${comment.authorId}`;
        name.textContent = comment.authorName || "Автор";
        head.append(who, name, el("span", "hint", formatDate(comment.createdAt)));
        if (comment.lastModifiedAt) {
            head.append(el("span", "hint", "изменено"));
        }

        return head;
    }

    function commentBody(comment, { reply = false } = {}) {
        const block = el("article", reply ? "comment comment--reply" : "comment");
        block.append(commentHead(comment));
        const mine = sameId(getSession()?.user?.userId, comment.authorId);
        if (editingId && sameId(editingId, comment.commentId)) {
            block.append(editForm(comment));
            return block;
        }

        block.append(el("p", "comment__text", comment.text));
        const actions = el("div", "comment__actions");
        if (!reply && getSession()) {
            actions.append(textButton(sameId(replyTo, comment.commentId) ? "Скрыть ответ" : "Ответить", () => {
                replyTo = sameId(replyTo, comment.commentId) ? null : comment.commentId;
                editingId = null;
                paint();
            }));
        }

        if (mine) {
            actions.append(textButton("Изменить", () => {
                editingId = comment.commentId;
                replyTo = null;
                paint();
            }));
            actions.append(textButton("Удалить", () => removeComment(comment)));
        }

        if (actions.childNodes.length) {
            block.append(actions);
        }

        return block;
    }

    function paint() {
        const tops = cache.filter((item) => !item.parentCommentId);
        const repliesOf = new Map();
        for (const item of cache) {
            if (!item.parentCommentId) {
                continue;
            }

            const key = String(item.parentCommentId).toLowerCase();
            const bucket = repliesOf.get(key) || [];
            bucket.push(item);
            repliesOf.set(key, bucket);
        }

        list.replaceChildren(composer(null));
        if (!tops.length) {
            list.append(createEmpty("Пока нет комментариев"));
            return;
        }

        for (const comment of tops) {
            const thread = el("div", "stack");
            thread.append(commentBody(comment));
            const replies = repliesOf.get(String(comment.commentId).toLowerCase()) || [];
            for (const reply of replies) {
                thread.append(commentBody(reply, { reply: true }));
            }

            if (sameId(replyTo, comment.commentId)) {
                thread.append(composer(comment.commentId));
            }

            list.append(thread);
        }
    }

    async function load() {
        try {
            const comments = await commentsApi.list(videoId);
            if (!alive) {
                return;
            }

            cache = Array.isArray(comments) ? comments : [];
            alert.hide();
            paint();
        } catch (error) {
            if (!alive) {
                return;
            }

            list.replaceChildren(createEmpty(error.message));
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
