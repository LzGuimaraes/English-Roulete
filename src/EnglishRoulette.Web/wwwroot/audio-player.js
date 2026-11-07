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
