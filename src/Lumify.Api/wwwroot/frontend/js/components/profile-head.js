import { el } from "../dom.js";
import { formatDate } from "../format.js";
import { createAvatar } from "./avatar.js";

export function createProfileHead(user, { showPrivate = false, hero = false } = {}) {
    const row = el("div", hero ? "profile profile--hero" : "profile");
    row.append(createAvatar(user, hero ? "lg" : "md"));

    const text = el("div", "profile__text");
    if (hero) {
        text.append(el("p", "eyebrow", showPrivate ? "Твой профиль" : "Автор"));
    }

    text.append(el("h1", "title profile__name", user.userName));

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
