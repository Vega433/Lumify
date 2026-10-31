import { el } from "../dom.js";

const failText = "Не получилось открыть поток. Если статус «Готово», на сервере должен лежать плейлист index.m3u8 в папке этого видео.";

export function createPlayer(src, poster) {
    const wrap = el("div", "player");
    const video = document.createElement("video");
    video.className = "player__video";
    video.controls = true;
    video.playsInline = true;
    video.setAttribute("playsinline", "");
    if (poster) {
        video.poster = poster;
    }

    const note = el("p", "player__note", "");
    note.hidden = true;
    wrap.append(video, note);

    let hls = null;

    function fail(text) {
        note.hidden = false;
        note.textContent = text;
    }

    if (!src) {
        fail("Ссылка на видео ещё не готова.");
    } else if (video.canPlayType("application/vnd.apple.mpegurl")) {
        video.src = src;
        video.addEventListener("error", () => fail(failText));
    } else if (window.Hls && window.Hls.isSupported()) {
        hls = new window.Hls();
        hls.loadSource(src);
        hls.attachMedia(video);
        hls.on(window.Hls.Events.ERROR, (_, data) => {
            if (!data?.fatal) {
                return;
            }

            fail(failText);
        });
    } else {
        fail("Этот браузер не умеет проигрывать HLS. Открой страницу в Chrome, Edge, Firefox или Safari. Библиотека hls.js подключается из index.html и нужен интернет.");
    }

    return {
        element: wrap,
        destroy() {
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
