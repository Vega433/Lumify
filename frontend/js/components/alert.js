import { el } from "../dom.js";

export function createAlert() {
    const element = el("p", "alert");
    element.hidden = true;

    return {
        element,
        show(message, kind = "error") {
            element.hidden = false;
            element.textContent = message;
            element.classList.toggle("alert--ok", kind === "ok");
        },
        hide() {
            element.hidden = true;
            element.textContent = "";
            element.classList.remove("alert--ok");
        }
    };
}
