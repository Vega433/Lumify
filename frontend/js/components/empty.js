import { el } from "../dom.js";

export function createEmpty(text) {
    return el("p", "empty", text);
}
