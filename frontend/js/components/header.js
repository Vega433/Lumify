import { authApi } from "../api.js";
import { el } from "../dom.js";
import { navigate } from "../navigation.js";
import { getSession } from "../session.js";

function isCurrent(hash) {
    const current = location.hash || "#/";
    if (hash === "#/") {
        return current === "#/" || current === "#";
    }

    return current === hash;
}

export function createHeader() {
    const header = el("header", "header");
    const inner = el("div", "header__inner");
    header.append(inner);

    function render() {
        inner.replaceChildren();

        const logo = document.createElement("a");
        logo.className = "logo";
        logo.href = "#/";
        logo.append(el("span", "logo__mark"), document.createTextNode("Lumify"));

        const nav = el("nav", "nav");
        const session = getSession();
        const links = [["#/", "Главная"]];
        if (session) {
            links.push(["#/me", "Моя страница"]);
        } else {
            links.push(["#/login", "Вход"], ["#/register", "Регистрация"]);
        }

        for (const [hash, text] of links) {
            const link = document.createElement("a");
            link.className = "nav__link";
            link.href = hash;
            link.textContent = text;
            if (isCurrent(hash)) {
                link.classList.add("is-active");
                link.setAttribute("aria-current", "page");
            }

            nav.append(link);
        }

        if (session) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "button button--ghost";
            button.textContent = "Выйти";
            button.addEventListener("click", async () => {
                button.disabled = true;
                await authApi.logout();
                navigate("#/");
            });
            nav.append(button);
        }

        inner.append(logo, nav);
    }

    window.addEventListener("hashchange", render);
    window.addEventListener("lumify:session", render);
    render();
    return header;
}
