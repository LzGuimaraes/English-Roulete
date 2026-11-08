// BFCache can restore an entire running page, including its WASM memory.
// Returning to a cached solo page must start a new visit, not revive a game.
window.addEventListener("pageshow", event => {
    if (event.persisted && document.querySelector("[data-solo-page]")) window.location.reload();
});
