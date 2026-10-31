import { el } from "../dom.js";
import { statusInfo } from "../format.js";

export function createStatusBadge(status, { hideReady = false } = {}) {
    const info = statusInfo(status);
    if (hideReady && info.key === "ready") {
        return null;
    }

    return el("span", `badge badge--${info.key}`, info.label);
}
