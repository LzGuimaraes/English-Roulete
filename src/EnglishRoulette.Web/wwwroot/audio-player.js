export async function play(audio) {
    await audio.play();
}

export function isReady(audio) {
    return audio.readyState >= 3;
}

export function stop(audio) {
    audio.pause();
}

export function reload(audio) {
    audio.load();
}

export function synchronize(audio, phase, offset) {
    if (phase !== "Narrating") {
        audio.pause();
        if (phase === "Preloading" && audio.readyState >= 1) audio.currentTime = 0;
        return;
    }
    const seek = () => {
        if (audio.paused && Number.isFinite(audio.duration)) {
            audio.currentTime = Math.max(0, Math.min(offset, audio.duration));
        }
    };
    if (audio.readyState >= 1) seek();
    else audio.addEventListener("loadedmetadata", seek, { once: true });
}

// Presentation follows the media clock, never the authoritative question deadline.
// Published recordings have no word timestamps: this is proportional text reveal.
export function createNarrationText(element, audioElementId, text) {
    const audio = document.getElementById(audioElementId);
    if (!audio) throw new Error("Narração indisponível para acompanhar o texto.");
    const characters = typeof Intl.Segmenter === "function"
        ? [...new Intl.Segmenter("en", { granularity: "grapheme" }).segment(text)].map(part => part.segment)
        : Array.from(text);
    const preference = window.matchMedia("(prefers-reduced-motion: reduce)");
    const editorialReducedMotion = Boolean(element.closest(".motion-none"));
    let phase = "Preloading", full = false, frame = 0, lastCount = -1, disposed = false;

    function render() {
        if (disposed) return;
        const duration = audio.duration;
        const progress = Number.isFinite(duration) && duration > 0
            ? Math.max(0, Math.min(1, audio.currentTime / duration)) : 0;
        const count = phase !== "Narrating" || full || preference.matches || editorialReducedMotion || audio.ended
            ? characters.length : Math.floor(characters.length * progress);
        if (count !== lastCount) {
            element.textContent = characters.slice(0, count).join("");
            lastCount = count;
        }
    }

    function stopFrame() {
        if (frame) cancelAnimationFrame(frame);
        frame = 0;
    }

    function tick() {
        frame = 0;
        refresh();
    }

    function refresh() {
        render();
        if (!disposed && phase === "Narrating" && !full && !preference.matches && !editorialReducedMotion && !audio.paused && !audio.ended && !audio.seeking && audio.readyState >= 3) {
            if (!frame) frame = requestAnimationFrame(tick);
        } else stopFrame();
    }

    const events = ["timeupdate", "playing", "pause", "seeking", "seeked", "loadedmetadata", "durationchange", "ended", "ratechange", "waiting"];
    for (const event of events) audio.addEventListener(event, refresh);
    preference.addEventListener("change", refresh);
    return {
        update(nextPhase, showFull) {
            phase = nextPhase;
            full = showFull;
            refresh();
        },
        dispose() {
            disposed = true;
            stopFrame();
            for (const event of events) audio.removeEventListener(event, refresh);
            preference.removeEventListener("change", refresh);
        }
    };
}
