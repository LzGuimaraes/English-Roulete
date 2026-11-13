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
function graphemes(text) {
    return typeof Intl.Segmenter === "function"
        ? [...new Intl.Segmenter("en", { granularity: "grapheme" }).segment(text)].map(part => part.segment)
        : Array.from(text);
}

// Preserve Blazor-owned hint buttons while changing only their presentation text.
function textRevealer(element) {
    const parts = [...element.querySelectorAll("[data-reveal-text]")].map(node => ({
        node, characters: graphemes(node.dataset.revealText), word: node.closest("[data-reveal-word]")
    }));
    return count => {
        for (const { node, characters, word } of parts) {
            const shown = Math.max(0, Math.min(count, characters.length));
            const text = characters.slice(0, shown).join("");
            if (node.textContent !== text) node.textContent = text;
            if (word) { word.inert = shown < characters.length; word.hidden = shown === 0; }
            count -= characters.length;
        }
    };
}

export function focusHint(root, key) {
    [...root.querySelectorAll(".narration-hint")].find(button =>
        button.dataset.hintKey === key && !button.disabled && !button.inert && button.getClientRects().length
    )?.focus({ preventScroll: true });
}

export function createNarrationText(element, audioElementId, text) {
    const audio = document.getElementById(audioElementId);
    if (!audio) throw new Error("Narração indisponível para acompanhar o texto.");
    const characters = graphemes(text), reveal = textRevealer(element);
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
            reveal(count);
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

// A trusted click unlocks this element before the shared server countdown.
export async function unlock(audio) {
    if (audio.networkState === HTMLMediaElement.NETWORK_EMPTY) audio.load();
    // Keep play() inside the click gesture. iOS Safari may defer loading until this call.
    await audio.play();
    audio.pause();
    audio.currentTime = 0;
    // Seeking back to zero can temporarily lower readyState even after playback succeeded.
    return true;
}
export function mute(audio, muted) { audio.muted = muted; }
export async function synchronizeShared(audio, phase, offset) {
    if (phase !== "Narrating") { synchronize(audio, phase, offset); return true; }
    if (audio.readyState < 3) return false;
    audio.currentTime = Math.max(0, Math.min(offset, audio.duration));
    try { await audio.play(); return true; } catch { return false; }
}

export function alignShared(audio, offset) {
    // Rebuffering/background tabs may lag. Never retry a blocked play without a gesture.
    if (!audio.paused && audio.readyState >= 3 && Number.isFinite(audio.duration) && Math.abs(audio.currentTime - offset) > .75) {
        audio.currentTime = Math.max(0, Math.min(offset, audio.duration));
    }
}

// Explicit editorial intervals follow the actual media position (including pause/seek).
// Within each cue, reveal is proportional; it is not phonetic word alignment.
export function createDialogueText(bubble, speaker, copy, audioElementId, cues) {
    const audio = document.getElementById(audioElementId);
    if (!audio) throw new Error("Áudio indisponível para acompanhar o diálogo.");
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
    const editorialReduced = Boolean(bubble.closest(".motion-none"));
    const segments = cues.map(cue => graphemes(cue.text));
    const lines = [...copy.querySelectorAll("[data-dialogue-cue]")];
    const revealers = lines.map(textRevealer);
    let phase, frame = 0, disposed = false, previous = -1, count = -1, entrance;
    function refresh() {
        if (disposed) return;
        const position = audio.currentTime * 1000;
        let index = cues.findLastIndex(cue => cue.startMilliseconds <= position);
        index = Math.max(0, index);
        const cue = cues[index], characters = segments[index];
        const fraction = Math.max(0, Math.min(1, (position - cue.startMilliseconds) / (cue.endMilliseconds - cue.startMilliseconds)));
        const next = reduced.matches || editorialReduced || audio.ended ? characters.length : Math.floor(characters.length * fraction);
        if (index !== previous) {
            for (let line = 0; line < lines.length; line++) {
                lines[line].hidden = line !== index;
                if (line !== index) revealers[line](0);
            }
            speaker.textContent = cue.speaker;
            entrance?.cancel();
            if (!reduced.matches && !editorialReduced) entrance = bubble.animate(
                [{ transform: "translateY(5px)" }, { transform: "translateY(0)" }],
                { duration: 260, easing: "cubic-bezier(.16,1,.3,1)" });
        }
        if (index !== previous || next !== count) revealers[index](next);
        previous = index; count = next;
        if (frame) cancelAnimationFrame(frame);
        frame = !disposed && phase === "Narrating" && !audio.paused && !audio.ended && !audio.seeking && audio.readyState >= 3
            ? requestAnimationFrame(refresh) : 0;
    }
    const events = ["timeupdate", "playing", "pause", "seeking", "seeked", "loadedmetadata", "ended", "waiting"];
    for (const event of events) audio.addEventListener(event, refresh);
    reduced.addEventListener("change", refresh);
    return {
        update(nextPhase) { phase = nextPhase; refresh(); },
        dispose() {
            disposed = true;
            if (frame) cancelAnimationFrame(frame);
            entrance?.cancel();
            for (const event of events) audio.removeEventListener(event, refresh);
            reduced.removeEventListener("change", refresh);
        }
    };
}
