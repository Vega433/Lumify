import { videosApi } from "../api.js";
import { el, setBusy } from "../dom.js";
import { createAlert } from "./alert.js";
import { createField } from "./field.js";

export function createUploadForm(onUploaded) {
    const panel = el("section", "panel stack");
    panel.append(el("h2", "section__title", "Загрузить видео"));

    const alert = createAlert();
    const form = document.createElement("form");
    form.className = "form";
    form.append(
        createField({ label: "Название", name: "title", required: true, maxLength: 200 }),
        createField({
            label: "Описание",
            name: "description",
            type: "textarea",
            maxLength: 1000
        }),
        createField({
            label: "Файл видео",
            name: "video",
            type: "file",
            required: true,
            accept: ".mp4,.mov,.mkv,.webm,.avi,video/*"
        }),
        createField({
            label: "Превью, можно не выбирать",
            name: "preview",
            type: "file",
            accept: "image/*"
        }),
        el("p", "hint", "Можно mp4, mov, mkv, webm или avi, до 512 МБ. Плеер появится, когда статус станет «Готово».")
    );

    const button = document.createElement("button");
    button.className = "button";
    button.type = "submit";
    button.textContent = "Загрузить";
    form.append(alert.element, button);
    panel.append(form);

    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        alert.hide();

        const title = form.querySelector("input[name=title]").value.trim();
        const description = form.querySelector("textarea[name=description]").value.trim();
        const file = form.querySelector("input[name=video]").files[0];
        const preview = form.querySelector("input[name=preview]").files[0];

        if (!file) {
            alert.show("Выбери видеофайл");
            return;
        }

        const payload = new FormData();
        payload.set("title", title);
        if (description) {
            payload.set("description", description);
        }

        payload.set("video", file);
        if (preview) {
            payload.set("preview", preview);
        }

        setBusy(button, true, "Загружаем…");
        try {
            await videosApi.upload(payload);
            form.reset();
            alert.show("Видео поставлено в очередь. Страница сама обновит статус.", "ok");
            await onUploaded();
        } catch (error) {
            alert.show(error.message);
        } finally {
            setBusy(button, false);
        }
    });

    return panel;
}
