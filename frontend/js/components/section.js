import { el } from "../dom.js";

export function createSection(title, content) {
    const section = el("section", "section");
    section.append(el("h2", "section__title", title), content);
    return section;
}
