import { API_ORIGIN } from "./config.js";
import { clearSession, getSession, saveSession } from "./session.js";

export class ApiError extends Error {
    constructor(status, message) {
        super(message);
        this.name = "ApiError";
        this.status = status;
    }
}

const knownMessages = new Map([
    ["Incorrect email or password", "Неверная почта или пароль"],
    ["Password must be at least 6 characters", "Пароль должен быть не короче 6 символов"],
    ["userName, email and password are required", "Нужны имя, почта и пароль"],
    ["title is required", "Нужно название"],
    ["file not selected", "Выбери видеофайл"],
    ["Unsupported video file extension", "Такой формат видео не принимается. Можно mp4, mov, mkv, webm или avi"],
    ["Unsupported file extension", "Такое изображение не принимается. Можно jpg, png или webp"],
    ["Invalid access token claims", "Сессия сломалась. Войди ещё раз"],
    ["The name must contain 4 or more characters.", "Имя должно быть не короче 4 символов"],
    ["Birth date cannot be in the future", "Дата рождения не может быть в будущем"]
]);

function translate(message, status) {
    const text = (message || "").trim();
    if (knownMessages.has(text)) {
        return knownMessages.get(text);
    }

    if (text.toLowerCase().includes("already registered")) {
        return "Такая почта или имя уже заняты";
    }

    if (text.includes("years old")) {
        return "Зарегистрироваться можно с 12 лет";
    }

    if (!text) {
        if (status === 401) {
            return "Нужно войти";
        }

        if (status === 403) {
            return "Это действие доступно только владельцу";
        }

        if (status === 404) {
            return "Не найдено";
        }

        return "Запрос не прошёл";
    }

    return text;
}

async function readError(response) {
    const text = await response.text();
    if (!text) {
        return translate("", response.status);
    }

    try {
        const json = JSON.parse(text);
        if (json.errors) {
            const first = Object.values(json.errors).flat()[0];
            if (first) {
                return translate(String(first), response.status);
            }
        }

        const message = json.detail || json.title || json.message || text;
        return translate(String(message), response.status);
    } catch {
        return translate(text, response.status);
    }
}

let refreshTask = null;

function refreshTokens() {
    const session = getSession();
    if (!session?.refreshToken) {
        return Promise.resolve(false);
    }

    if (!refreshTask) {
        refreshTask = fetch(`${API_ORIGIN}/api/auth/refresh`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken: session.refreshToken }),
            cache: "no-store"
        }).then(async (response) => {
            if (!response.ok) {
                clearSession();
                return false;
            }

            const data = await response.json();
            if (!data?.accessToken) {
                clearSession();
                return false;
            }

            saveSession(data);
            return true;
        }).catch(() => {
            clearSession();
            return false;
        }).finally(() => {
            refreshTask = null;
        });
    }

    return refreshTask;
}

export async function api(path, { method = "GET", json, form, auth = true, retry = true } = {}) {
    const headers = new Headers();
    const session = getSession();

    if (auth && session?.accessToken) {
        headers.set("Authorization", `Bearer ${session.accessToken}`);
    }

    let body;
    if (json !== undefined) {
        headers.set("Content-Type", "application/json");
        body = JSON.stringify(json);
    } else if (form) {
        body = form;
    }

    let response;
    try {
        response = await fetch(`${API_ORIGIN}${path}`, { method, headers, body, cache: "no-store" });
    } catch {
        throw new ApiError(0, `Нет связи с API (${API_ORIGIN}). Открой сайт по адресу бэкенда, путь /app/.`);
    }

    if (response.status === 401 && auth && retry && session?.refreshToken) {
        const refreshed = await refreshTokens();
        if (refreshed) {
            return api(path, { method, json, form, auth, retry: false });
        }
    }

    if (!response.ok) {
        throw new ApiError(response.status, await readError(response));
    }

    if (response.status === 204) {
        return null;
    }

    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
        return response.json();
    }

    return null;
}

export const authApi = {
    async login(email, password) {
        const auth = await api("/api/auth/login", {
            method: "POST",
            json: { email, password },
            auth: false
        });
        saveSession(auth);
        return auth;
    },
    register(form) {
        return api("/api/auth/register", { method: "POST", form, auth: false });
    },
    async logout() {
        try {
            await api("/api/auth/logout", { method: "POST" });
        } catch {
            // Токены всё равно стираем: сервер мог уже не принять access.
        }

        clearSession();
    }
};

export const videosApi = {
    feed(limit = 24) {
        return api(`/api/videos?limit=${limit}`);
    },
    byId(id) {
        return api(`/api/videos/${id}`);
    },
    byAuthor(id) {
        return api(`/api/videos/author/${id}`);
    },
    upload(form) {
        return api("/api/videos", { method: "POST", form });
    },
    remove(id) {
        return api(`/api/videos/${id}`, { method: "DELETE" });
    }
};

export const usersApi = {
    list() {
        return api("/api/users");
    },
    byId(id) {
        return api(`/api/users/${id}`);
    },
    me() {
        return api("/api/users/me");
    }
};
