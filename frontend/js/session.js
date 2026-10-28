// Сессия живёт в localStorage браузера, не в cookie. Ключ один, внутри access, refresh и профиль.
const storageKey = "lumify.session";

export function getSession() {
    const raw = localStorage.getItem(storageKey);
    if (!raw) {
        return null;
    }

    try {
        const session = JSON.parse(raw);
        if (!session?.accessToken || !session?.user) {
            return null;
        }

        return session;
    } catch {
        return null;
    }
}

export function saveSession(auth) {
    localStorage.setItem(storageKey, JSON.stringify({
        accessToken: auth.accessToken,
        refreshToken: auth.refreshToken,
        user: auth.user
    }));
    window.dispatchEvent(new Event("lumify:session"));
}

export function clearSession() {
    localStorage.removeItem(storageKey);
    window.dispatchEvent(new Event("lumify:session"));
}
