import { el } from "../dom.js";
import { formatDate } from "../format.js";
import { createAvatar } from "./avatar.js";

export function createProfileHead(user, { showPrivate = false } = {}) {
    const row = el("div", "profile");
    row.append(createAvatar(user));

    const text = el("div", "profile__text");
    text.append(el("h1", "title", user.userName));

    if (showPrivate && user.email) {
        text.append(el("p", "muted", user.email));
    }

    const bits = [];
    if (user.createdAt) {
        bits.push(`На сайте с ${formatDate(user.createdAt)}`);
    }

    if (showPrivate && user.birthDate) {
        bits.push(`дата рождения ${formatDate(user.birthDate)}`);
    }

    if (bits.length) {
        text.append(el("p", "muted", bits.join(" · ")));
    }

    row.append(text);
    return row;
}
