import { el } from "./dom.js";
import { navigate, rememberNext } from "./navigation.js";
import { getSession } from "./session.js";
import { mountHomePage } from "./pages/home-page.js";
import { mountLoginPage } from "./pages/login-page.js";
import { mountMePage } from "./pages/me-page.js";
import { mountRegisterPage } from "./pages/register-page.js";
import { mountUserPage } from "./pages/user-page.js";
import { mountVideoPage } from "./pages/video-page.js";

const routes = [
    { pattern: /^#\/?$/, title: "Главная", auth: false, mount: () => mountHomePage() },
    { pattern: /^#\/video\/([0-9a-fA-F-]{36})\/?$/, title: "Видео", auth: false, mount: (params) => mountVideoPage(params[0]) },
    { pattern: /^#\/user\/([0-9a-fA-F-]{36})\/?$/, title: "Автор", auth: false, mount: (params) => mountUserPage(params[0]) },
    { pattern: /^#\/me\/?$/, title: "Моя страница", auth: true, mount: () => mountMePage() },
    { pattern: /^#\/login\/?$/, title: "Вход", auth: false, mount: () => mountLoginPage() },
    { pattern: /^#\/register\/?$/, title: "Регистрация", auth: false, mount: () => mountRegisterPage() }
];

function matchRoute(hash) {
    for (const route of routes) {
        const found = hash.match(route.pattern);
        if (found) {
            return { route, params: found.slice(1) };
        }
    }

    return null;
}

function mountMissing() {
    const element = el("div", "stack");
    element.append(el("h1", "title", "Такой страницы нет"));
    const link = document.createElement("a");
    link.className = "text-link";
    link.href = "#/";
    link.textContent = "На главную";
    element.append(link);
    return { element, destroy() { } };
}

export function startRouter(outlet) {
    let current = null;

    function render() {
        const hash = location.hash || "#/";
        if (current) {
            current.destroy();
            current = null;
        }

        if ((hash.startsWith("#/login") || hash.startsWith("#/register")) && getSession()) {
            navigate("#/");
            return;
        }

        const found = matchRoute(hash);
        if (found?.route.auth && !getSession()) {
            rememberNext(hash);
            navigate("#/login");
            return;
        }

        const view = found ? found.route.mount(found.params) : mountMissing();
        current = view;
        document.title = found ? `${found.route.title} · Lumify` : "Страница не найдена · Lumify";
        outlet.replaceChildren(view.element);
        window.scrollTo(0, 0);
    }

    window.addEventListener("hashchange", render);
    render();
}
