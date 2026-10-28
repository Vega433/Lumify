const nextKey = "lumify.next";

export function navigate(hash) {
    const target = hash.startsWith("#") ? hash : `#${hash}`;
    if (location.hash === target) {
        window.dispatchEvent(new HashChangeEvent("hashchange"));
        return;
    }

    location.hash = target;
}

export function rememberNext(hash) {
    sessionStorage.setItem(nextKey, hash);
}

export function takeNext() {
    const next = sessionStorage.getItem(nextKey);
    sessionStorage.removeItem(nextKey);
    if (!next || next === "#/login" || next === "#/register") {
        return "#/";
    }

    return next;
}
