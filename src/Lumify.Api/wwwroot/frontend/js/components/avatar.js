import { mediaUrl } from "../config.js";
import { el } from "../dom.js";

const avatarClass = {
    sm: "avatar avatar--sm",
    lg: "avatar avatar--lg"
};

export function createAvatar(user, size = "md") {
    const node = el("div", avatarClass[size] || "avatar");
    if (user?.avatarUrl) {
        const img = document.createElement("img");
        img.src = mediaUrl(user.avatarUrl);
        img.alt = "";
        node.append(img);
        return node;
    }

    const name = user?.userName || "?";
    node.textContent = name.slice(0, 1).toUpperCase();
    return node;
}
