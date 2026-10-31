import { el } from "../dom.js";

const failText = "Не получилось открыть поток. Если статус «Готово», на сервере должен лежать плейлист index.m3u8 в папке этого видео.";
const qualityKey = "lumify.player.quality";
const volumeKey = "lumify.player.volume";
const mutedKey = "lumify.player.muted";
const svgNs = "http://www.w3.org/2000/svg";

export function createPlayer(src, poster) {
    const wrap = el("div", "player is-paused");
    wrap.tabIndex = 0;

    const stage = el("div", "player__stage");
    const video = document.createElement("video");
    video.className = "player__video";
    video.playsInline = true;
    video.setAttribute("playsinline", "");
    video.preload = "metadata";
    if (poster) {
        video.poster = poster;
    }

    const spinner = el("div", "player__spinner");
    spinner.hidden = true;

    const center = button("player__center", "Воспроизвести");
    center.append(icon("play"));

    const controls = el("div", "player__controls");
    const seek = document.createElement("input");
    seek.className = "player__seek";
    seek.type = "range";
    seek.min = "0";
    seek.max = "1000";
    seek.value = "0";
    seek.setAttribute("aria-label", "Позиция");
    const timeline = el("div", "player__timeline");
    timeline.append(seek);

    const playButton = button("player__btn", "Воспроизвести");
    playButton.append(icon("play"));

    const time = el("span", "player__time", "0:00 / 0:00");

    const muteButton = button("player__btn", "Выключить звук");
    muteButton.append(icon("volume"));

    const volume = document.createElement("input");
    volume.className = "player__volume";
    volume.type = "range";
    volume.min = "0";
    volume.max = "1";
    volume.step = "0.05";
    volume.value = "1";
    volume.setAttribute("aria-label", "Громкость");

    const qualityWrap = el("div", "player__quality");
    const qualityButton = button("player__btn player__btn--quality", "Качество");
    qualityButton.setAttribute("aria-haspopup", "menu");
    qualityButton.setAttribute("aria-expanded", "false");
    const qualityLabel = el("span", "player__quality-label", "Авто");
    qualityButton.append(qualityLabel, icon("caret"));

    const menu = el("div", "player__menu");
    menu.hidden = true;
    menu.setAttribute("role", "menu");
    menu.setAttribute("aria-label", "Качество");
    qualityWrap.append(qualityButton, menu);

    const fullButton = button("player__btn", "Полный экран");
    fullButton.append(icon("fullscreen"));

    const bar = el("div", "player__bar");
    bar.append(playButton, time, muteButton, volume, el("span", "player__grow"), qualityWrap, fullButton);
    controls.append(timeline, bar);

    const note = el("p", "player__note", "");
    note.hidden = true;
    stage.append(video, spinner, center, controls);
    wrap.append(stage, note);

    const events = new AbortController();
    const signal = events.signal;
    let hls = null;
    let levels = [];
    let choice = "auto";
    let currentIndex = -1;
    let scrubbing = false;
    let hideTimer = 0;
    let nativeMaster = "";

    function fail(text) {
        note.hidden = false;
        note.textContent = text;
        spinner.hidden = true;
    }

    function readNumber(key) {
        try {
            const raw = localStorage.getItem(key);
            if (raw == null || raw === "auto") {
                return null;
            }

            const value = Number(raw);
            return Number.isFinite(value) ? value : null;
        } catch {
            return null;
        }
    }

    function remember(key, value) {
        try {
            localStorage.setItem(key, String(value));
        } catch {
            // Настройка просто не запомнится.
        }
    }

    function showChrome() {
        wrap.classList.add("is-active");
        window.clearTimeout(hideTimer);
        if (!video.paused && menu.hidden) {
            hideTimer = window.setTimeout(() => wrap.classList.remove("is-active"), 2400);
        }
    }

    function setPaused(paused) {
        wrap.classList.toggle("is-paused", paused);
        wrap.classList.toggle("is-playing", !paused);
        playButton.replaceChildren(icon(paused ? "play" : "pause"));
        playButton.setAttribute("aria-label", paused ? "Воспроизвести" : "Пауза");
        center.setAttribute("aria-label", paused ? "Воспроизвести" : "Пауза");
        if (paused) {
            showChrome();
        }
    }

    function togglePlay() {
        if (video.paused) {
            video.play().catch(() => fail(failText));
        } else {
            video.pause();
        }
    }

    function paintSeek(seconds) {
        const duration = video.duration;
        const played = duration ? Math.min(100, (seconds / duration) * 100) : 0;
        let buffered = played;
        for (let i = 0; i < video.buffered.length; i++) {
            if (video.buffered.start(i) <= seconds && seconds <= video.buffered.end(i)) {
                buffered = duration ? Math.min(100, (video.buffered.end(i) / duration) * 100) : played;
                break;
            }
        }

        seek.style.setProperty("--played", `${played}%`);
        seek.style.setProperty("--buffered", `${buffered}%`);
        if (!scrubbing && duration) {
            seek.value = String(Math.round((seconds / duration) * 1000));
        }

        time.textContent = `${formatClock(seconds)} / ${formatClock(duration)}`;
    }

    function paintVolume() {
        const amount = video.muted ? 0 : video.volume;
        const silent = amount === 0;
        muteButton.replaceChildren(icon(silent ? "muted" : "volume"));
        muteButton.setAttribute("aria-label", silent ? "Включить звук" : "Выключить звук");
        volume.value = String(amount);
        volume.style.setProperty("--played", `${amount * 100}%`);
    }

    function activeLevel() {
        return levels.find(level => level.index === currentIndex) || null;
    }

    function paintQuality() {
        const active = activeLevel();
        if (choice === "auto") {
            qualityLabel.textContent = active ? `Авто · ${active.label}` : "Авто";
        } else {
            qualityLabel.textContent = active?.label || "Качество";
        }
    }

    function closeMenu() {
        menu.hidden = true;
        qualityButton.setAttribute("aria-expanded", "false");
        wrap.classList.remove("is-menu-open");
    }

    function openMenu() {
        menu.hidden = false;
        qualityButton.setAttribute("aria-expanded", "true");
        wrap.classList.add("is-menu-open");
        showChrome();
    }

    function renderMenu() {
        menu.replaceChildren(el("p", "player__menu-title", "Качество"));
        const rows = [
            { id: "auto", label: "Авто", hint: "Под сеть" },
            ...[...levels].sort((a, b) => (b.height - a.height) || (b.bitrate - a.bitrate))
        ];

        if (levels.length === 0) {
            qualityButton.disabled = true;
            return;
        }

        qualityButton.disabled = false;
        for (const row of rows) {
            const selected = row.id === "auto" ? choice === "auto" : choice === row.index;
            const item = button("player__option", row.label);
            item.setAttribute("role", "menuitemradio");
            item.setAttribute("aria-checked", selected ? "true" : "false");
            item.classList.toggle("is-selected", selected);
            let meta = row.id === "auto" ? row.hint : "";
            if (selected) {
                meta = "✓";
            } else if (choice === "auto" && row.index === currentIndex) {
                meta = "сейчас";
            }

            item.append(
                el("span", "player__option-label", row.label),
                el("span", "player__option-meta", meta)
            );
            item.addEventListener("click", () => {
                if (row.id === "auto") {
                    selectAuto();
                } else {
                    selectLevel(row);
                }

                closeMenu();
            }, { signal });
            menu.append(item);
        }

        paintQuality();
    }

    function selectAuto() {
        choice = "auto";
        remember(qualityKey, "auto");
        if (hls) {
            hls.currentLevel = -1;
        } else if (nativeMaster && video.src !== nativeMaster) {
            swapNativeSource(nativeMaster);
        }

        renderMenu();
    }

    function selectLevel(level, persist) {
        choice = level.index;
        if (persist !== false && level.height > 0) {
            remember(qualityKey, level.height);
        }

        if (hls) {
            hls.currentLevel = level.index;
            currentIndex = level.index;
        } else if (level.url) {
            currentIndex = level.index;
            swapNativeSource(level.url);
        }

        renderMenu();
    }

    function applyPreferred() {
        const wanted = readNumber(qualityKey);
        if (wanted == null) {
            choice = "auto";
            if (hls) {
                hls.currentLevel = -1;
            }

            renderMenu();
            return;
        }

        const match = [...levels]
            .filter(level => level.height > 0 && level.height <= wanted)
            .sort((a, b) => b.height - a.height)[0];
        if (match) {
            selectLevel(match, false);
            return;
        }

        choice = "auto";
        renderMenu();
    }

    function swapNativeSource(url) {
        const time = video.currentTime || 0;
        const paused = video.paused;
        video.src = url;
        video.addEventListener("loadedmetadata", () => {
            if (Number.isFinite(time)) {
                video.currentTime = Math.min(time, video.duration || time);
            }

            if (!paused) {
                video.play().catch(() => { });
            }
        }, { once: true });
    }

    function showSingleQuality(label) {
        qualityLabel.textContent = label;
        qualityButton.disabled = false;
        menu.replaceChildren(
            el("p", "player__menu-title", "Качество"),
            el("p", "player__menu-note", "В этом ролике одно качество.")
        );
    }

    function setLevels(next) {
        if (next.length === 0) {
            levels = [];
            showSingleQuality("Исходное");
            return;
        }

        const counts = new Map();
        for (const level of next) {
            counts.set(level.label, (counts.get(level.label) || 0) + 1);
        }

        for (const level of next) {
            if (counts.get(level.label) > 1 && level.bitrate > 0) {
                level.label += ` · ${Math.round(level.bitrate / 1000)} кбит/с`;
            }
        }

        levels = next;
        if (levels.length < 2) {
            showSingleQuality(levels[0]?.label || "Исходное");
            return;
        }

        qualityButton.disabled = false;
        applyPreferred();
    }

    function toggleFullscreen() {
        if (document.fullscreenElement === wrap) {
            document.exitFullscreen?.();
            return;
        }

        if (wrap.requestFullscreen) {
            wrap.requestFullscreen();
            return;
        }

        video.webkitEnterFullscreen?.();
    }

    function paintFullscreen() {
        const active = document.fullscreenElement === wrap;
        fullButton.replaceChildren(icon(active ? "exit" : "fullscreen"));
        fullButton.setAttribute("aria-label", active ? "Выйти из полного экрана" : "Полный экран");
    }

    function onKey(event) {
        const tag = event.target?.tagName;
        if (event.key === "Escape") {
            closeMenu();
        }

        if (tag === "INPUT" || tag === "BUTTON") {
            return;
        }

        if (event.key === " " || event.key === "k" || event.key === "K") {
            event.preventDefault();
            togglePlay();
        } else if (event.key === "ArrowRight") {
            event.preventDefault();
            video.currentTime = Math.min(video.duration || video.currentTime + 5, video.currentTime + 5);
        } else if (event.key === "ArrowLeft") {
            event.preventDefault();
            video.currentTime = Math.max(0, video.currentTime - 5);
        } else if (event.key === "ArrowUp") {
            event.preventDefault();
            video.muted = false;
            video.volume = Math.min(1, video.volume + 0.05);
        } else if (event.key === "ArrowDown") {
            event.preventDefault();
            video.volume = Math.max(0, video.volume - 0.05);
        } else if (event.key === "m" || event.key === "M") {
            event.preventDefault();
            video.muted = !video.muted;
        } else if (event.key === "f" || event.key === "F") {
            event.preventDefault();
            toggleFullscreen();
        }
    }

    playButton.addEventListener("click", togglePlay, { signal });
    center.addEventListener("click", (event) => {
        event.stopPropagation();
        togglePlay();
    }, { signal });
    video.addEventListener("click", () => {
        togglePlay();
        wrap.focus({ preventScroll: true });
    }, { signal });
    video.addEventListener("dblclick", toggleFullscreen, { signal });
    video.addEventListener("play", () => setPaused(false), { signal });
    video.addEventListener("pause", () => setPaused(true), { signal });
    video.addEventListener("timeupdate", () => paintSeek(video.currentTime || 0), { signal });
    video.addEventListener("durationchange", () => paintSeek(video.currentTime || 0), { signal });
    video.addEventListener("progress", () => paintSeek(video.currentTime || 0), { signal });
    video.addEventListener("volumechange", () => {
        paintVolume();
        remember(volumeKey, video.volume);
        remember(mutedKey, video.muted ? "1" : "0");
    }, { signal });
    video.addEventListener("waiting", () => {
        spinner.hidden = video.paused;
    }, { signal });
    video.addEventListener("playing", () => {
        spinner.hidden = true;
    }, { signal });
    video.addEventListener("canplay", () => {
        spinner.hidden = true;
    }, { signal });
    video.addEventListener("error", () => fail(failText), { signal });

    seek.addEventListener("pointerdown", () => {
        scrubbing = true;
    }, { signal });
    seek.addEventListener("pointerup", () => {
        scrubbing = false;
    }, { signal });
    seek.addEventListener("input", () => {
        const duration = video.duration || 0;
        const next = (Number(seek.value) / 1000) * duration;
        paintSeek(next);
        if (duration) {
            video.currentTime = next;
        }
    }, { signal });

    muteButton.addEventListener("click", () => {
        video.muted = !video.muted;
    }, { signal });
    volume.addEventListener("input", () => {
        video.volume = Number(volume.value);
        video.muted = video.volume === 0;
    }, { signal });
    qualityButton.addEventListener("click", () => {
        if (menu.hidden) {
            openMenu();
        } else {
            closeMenu();
        }
    }, { signal });
    fullButton.addEventListener("click", toggleFullscreen, { signal });
    wrap.addEventListener("pointermove", showChrome, { signal });
    wrap.addEventListener("pointerdown", showChrome, { signal });
    wrap.addEventListener("keydown", onKey, { signal });
    document.addEventListener("pointerdown", (event) => {
        if (!qualityWrap.contains(event.target)) {
            closeMenu();
        }
    }, { signal });
    document.addEventListener("fullscreenchange", paintFullscreen, { signal });

    const savedVolume = readNumber(volumeKey);
    if (savedVolume != null) {
        video.volume = Math.min(1, Math.max(0, savedVolume));
    }

    try {
        video.muted = localStorage.getItem(mutedKey) === "1";
    } catch {
        video.muted = false;
    }

    paintVolume();
    renderMenu();
    setPaused(true);

    if (!src) {
        fail("Ссылка на видео ещё не готова.");
    } else if (window.Hls && window.Hls.isSupported()) {
        hls = new window.Hls({
            capLevelToPlayerSize: true,
            startLevel: -1
        });
        hls.on(window.Hls.Events.MANIFEST_PARSED, () => {
            setLevels(hls.levels.map((level, index) => ({
                index,
                height: level.height || 0,
                bitrate: level.bitrate || 0,
                label: qualityName(level.height, level.bitrate)
            })));
        });
        hls.on(window.Hls.Events.LEVEL_SWITCHED, (_, data) => {
            currentIndex = data.level;
            paintQuality();
            renderMenu();
        });
        hls.on(window.Hls.Events.ERROR, (_, data) => {
            if (data?.fatal) {
                fail(failText);
            }
        });
        hls.loadSource(src);
        hls.attachMedia(video);
    } else if (video.canPlayType("application/vnd.apple.mpegurl")) {
        nativeMaster = src;
        video.src = src;
        loadNativeQualities(src).then(parsed => {
            if (events.signal.aborted) {
                return;
            }

            setLevels(parsed);
        }).catch(() => {
            setLevels([]);
        });
    } else {
        fail("Этот браузер не умеет проигрывать HLS. Открой страницу в Chrome, Edge, Firefox или Safari. Библиотека hls.js подключается из index.html и нужен интернет.");
    }

    return {
        element: wrap,
        destroy() {
            window.clearTimeout(hideTimer);
            events.abort();
            closeMenu();
            if (hls) {
                hls.destroy();
                hls = null;
            }

            video.pause();
            video.removeAttribute("src");
            video.load();
        }
    };
}

async function loadNativeQualities(src) {
    const response = await fetch(src);
    if (!response.ok) {
        return [];
    }

    const text = await response.text();
    if (!text.includes("#EXT-X-STREAM-INF")) {
        return [];
    }

    const lines = text.split(/\r?\n/);
    const levels = [];
    for (let i = 0; i < lines.length; i++) {
        if (!lines[i].startsWith("#EXT-X-STREAM-INF")) {
            continue;
        }

        const resolution = /RESOLUTION=\d+x(\d+)/.exec(lines[i]);
        const bandwidth = /BANDWIDTH=(\d+)/.exec(lines[i]);
        let uri = "";
        for (let j = i + 1; j < lines.length; j++) {
            const candidate = lines[j].trim();
            if (candidate && !candidate.startsWith("#")) {
                uri = candidate;
                break;
            }
        }

        if (!uri) {
            continue;
        }

        const height = resolution ? Number(resolution[1]) : 0;
        const bitrate = bandwidth ? Number(bandwidth[1]) : 0;
        levels.push({
            index: levels.length,
            height,
            bitrate,
            url: new URL(uri, src).href,
            label: qualityName(height, bitrate)
        });
    }

    return levels;
}

function qualityName(height, bitrate) {
    if (height >= 1) {
        return `${height}p`;
    }

    if (bitrate > 0) {
        return `${Math.round(bitrate / 1000)} кбит/с`;
    }

    return "Исходное";
}

function formatClock(seconds) {
    if (!Number.isFinite(seconds) || seconds < 0) {
        return "0:00";
    }

    const whole = Math.floor(seconds);
    const hours = Math.floor(whole / 3600);
    const minutes = Math.floor((whole % 3600) / 60);
    const rest = whole % 60;
    if (hours > 0) {
        return `${hours}:${String(minutes).padStart(2, "0")}:${String(rest).padStart(2, "0")}`;
    }

    return `${minutes}:${String(rest).padStart(2, "0")}`;
}

function button(className, label) {
    const node = document.createElement("button");
    node.type = "button";
    node.className = className;
    node.setAttribute("aria-label", label);
    return node;
}

function icon(name) {
    const specs = {
        play: [["M8 5.14v13.72L19 12z", "fill"]],
        pause: [["M6 5h3.5v14H6zM14.5 5H18v14h-3.5z", "fill"]],
        caret: [["M7 9.5 12 14.5 17 9.5", "stroke"]],
        volume: [
            ["M4 9.5h3.4L12 5.2v13.6L7.4 14.5H4z", "fill"],
            ["M15.5 9.2a3.8 3.8 0 0 1 0 5.6", "stroke"],
            ["M17.6 7a6.4 6.4 0 0 1 0 10", "stroke"]
        ],
        muted: [
            ["M4 9.5h3.4L12 5.2v13.6L7.4 14.5H4z", "fill"],
            ["M16 10l4 4M20 10l-4 4", "stroke"]
        ],
        fullscreen: [["M4 9V4h5M15 4h5v5M20 15v5h-5M9 20H4v-5", "stroke"]],
        exit: [["M9 4v5H4M15 4v5h5M20 15h-5v5M4 15h5v5", "stroke"]]
    };

    const svg = document.createElementNS(svgNs, "svg");
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("aria-hidden", "true");
    svg.classList.add("player__icon");
    for (const [d, mode] of specs[name] || specs.play) {
        const path = document.createElementNS(svgNs, "path");
        path.setAttribute("d", d);
        path.setAttribute("fill", mode === "fill" ? "currentColor" : "none");
        if (mode === "stroke") {
            path.setAttribute("stroke", "currentColor");
            path.setAttribute("stroke-width", "1.8");
            path.setAttribute("stroke-linecap", "round");
            path.setAttribute("stroke-linejoin", "round");
        }

        svg.append(path);
    }

    return svg;
}
