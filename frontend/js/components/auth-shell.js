import { el } from "../dom.js";

export function createAuthShell({ title, text, footer }) {
    const page = el("div", "auth");
    const panel = el("section", "panel stack");
    const form = document.createElement("form");
    form.className = "form";
    panel.append(el("h1", "title", title), el("p", "muted", text), form);
    if (footer) {
        panel.append(footer);
    }

    page.append(panel);
    return { element: page, form };
}
