export async function shareInvite(url) {
    if (navigator.share) {
        try {
            await navigator.share({ title: "English Roulette", text: "Jogue comigo!", url });
            return "shared";
        } catch (error) {
            if (error.name === "AbortError") return "cancelled";
            throw error;
        }
    }
    await navigator.clipboard.writeText(url);
    return "copied";
}
