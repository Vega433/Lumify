const statuses = {
    0: { key: "created", label: "Создано" },
    1: { key: "pending", label: "В очереди" },
    2: { key: "processing", label: "Обработка" },
    3: { key: "ready", label: "Готово" },
    4: { key: "failed", label: "Ошибка" },
    Created: { key: "created", label: "Создано" },
    Pending: { key: "pending", label: "В очереди" },
    Processing: { key: "processing", label: "Обработка" },
    Ready: { key: "ready", label: "Готово" },
    Failed: { key: "failed", label: "Ошибка" }
};

export function statusInfo(status) {
    return statuses[status] || { key: "unknown", label: "Неизвестно" };
}

export function isReady(status) {
    return status === 3 || status === "Ready";
}

export function isFailed(status) {
    return status === 4 || status === "Failed";
}

export function isProcessing(status) {
    return !isReady(status) && !isFailed(status);
}

export function formatDuration(value) {
    if (value === null || value === undefined || value === "") {
        return "0:00";
    }

    const text = String(value);
    let days = 0;
    let clock = text;
    const dayMatch = text.match(/^(\d+)\.(\d{2}:\d{2}:\d{2}(?:\.\d+)?)$/);
    if (dayMatch) {
        days = Number(dayMatch[1]);
        clock = dayMatch[2];
    }

    const match = clock.match(/^(\d+):(\d+):(\d+)(?:\.\d+)?$/);
    if (!match) {
        return text;
    }

    const hours = Number(match[1]) + days * 24;
    const minutes = Number(match[2]);
    const seconds = Number(match[3]);
    const ss = String(seconds).padStart(2, "0");
    if (hours > 0) {
        return `${hours}:${String(minutes).padStart(2, "0")}:${ss}`;
    }

    return `${minutes}:${ss}`;
}

export function hasDuration(value) {
    return formatDuration(value) !== "0:00";
}

export function formatDate(value) {
    if (typeof value === "string" && /^\d{4}-\d{2}-\d{2}/.test(value)) {
        const [year, month, day] = value.slice(0, 10).split("-").map(Number);
        return new Date(year, month - 1, day).toLocaleDateString("ru-RU", {
            day: "numeric",
            month: "short",
            year: "numeric"
        });
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return "";
    }

    return date.toLocaleDateString("ru-RU", {
        day: "numeric",
        month: "short",
        year: "numeric"
    });
}

function plural(count, one, few, many) {
    const n = Math.abs(count) % 100;
    const n1 = n % 10;
    if (n > 10 && n < 20) {
        return many;
    }

    if (n1 > 1 && n1 < 5) {
        return few;
    }

    if (n1 === 1) {
        return one;
    }

    return many;
}

export function formatCount(count) {
    return (Number(count) || 0).toLocaleString("ru-RU");
}

export function formatViews(count) {
    const value = Number(count) || 0;
    return `${formatCount(value)} ${plural(value, "просмотр", "просмотра", "просмотров")}`;
}

export function formatLikes(count) {
    const value = Number(count) || 0;
    return `${formatCount(value)} ${plural(value, "лайк", "лайка", "лайков")}`;
}

export function sameId(left, right) {
    return String(left || "").toLowerCase() === String(right || "").toLowerCase();
}
