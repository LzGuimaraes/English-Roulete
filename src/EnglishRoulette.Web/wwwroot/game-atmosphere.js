// Original, quiet procedural soundtrack. No downloaded media or runtime services.
let context, master, timer;
let enabled = false, horror = false, narrating = false, step = 0;
let focusBeforeHelp;
const activeVoices = new Set();

export function showWheel(element) {
    if (innerWidth < 780) element.scrollIntoView({block: "center", behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth"});
}
export function openHelp(dialog) {
    focusBeforeHelp = document.activeElement;
    dialog.showModal();
}
export function closeHelp(dialog) {
    dialog.close();
    focusBeforeHelp?.focus();
}
function tone(frequency, when, duration, volume, type = "sine") {
    const oscillator = context.createOscillator();
    const envelope = context.createGain();
    oscillator.type = type;
    oscillator.frequency.value = frequency;
    envelope.gain.setValueAtTime(0, when);
    envelope.gain.linearRampToValueAtTime(volume, when + .18);
    envelope.gain.exponentialRampToValueAtTime(.0001, when + duration);
    oscillator.connect(envelope).connect(master);
    activeVoices.add(oscillator);
    oscillator.onended = () => { oscillator.disconnect(); envelope.disconnect(); activeVoices.delete(oscillator); };
    oscillator.start(when);
    oscillator.stop(when + duration + .05);
}
function phrase() {
    if (!enabled || document.hidden || context.state !== "running") return;
    const now = context.currentTime + .03;
    // A soft minor arpeggio; the horror mood uses a low, dissonant drone.
    const notes = horror ? [55, 58.27, 82.41, 55] : [220, 261.63, 329.63, 392, 329.63, 261.63, 196, 246.94];
    tone(notes[step % notes.length], now, horror ? 3.6 : 2.6, horror ? .17 : .15, horror ? "triangle" : "sine");
    if (step % 4 === 0) tone(horror ? 41.2 : 110, now, 4.2, .1);
    step++;
}
function updateVolume() {
    if (master) master.gain.setTargetAtTime(enabled && !document.hidden ? (narrating ? .035 : .22) : 0, context.currentTime, .25);
}
export function setMood(isHorror, isNarrating) {
    horror = isHorror; narrating = isNarrating; updateVolume();
}
export async function toggleSound(isHorror, isNarrating) {
    if (enabled) { stopSound(); return false; }
    context ??= new AudioContext();
    if (!master) { master = context.createGain(); master.gain.value = 0; master.connect(context.destination); }
    await context.resume(); // Only called directly from a user gesture.
    enabled = true; step = 0;
    setMood(isHorror, isNarrating);
    phrase(); timer = setInterval(phrase, 760);
    document.addEventListener("visibilitychange", updateVolume);
    return true;
}
export function stopSound() {
    enabled = false;
    clearInterval(timer); timer = undefined;
    updateVolume();
    for (const voice of activeVoices) voice.stop();
    activeVoices.clear();
    document.removeEventListener("visibilitychange", updateVolume);
}
