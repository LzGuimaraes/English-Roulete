// The test runner may inspect a live page's credential, but never restores it.
const assert = require('node:assert/strict');
const credentials = new WeakMap();
const watched = new WeakSet();
function watchSolo(page) {
    if (watched.has(page)) return;
    watched.add(page);
    page.on('framenavigated', frame => { if (frame === page.mainFrame()) credentials.delete(page); });
    page.on('response', async response => {
        if (new URL(response.url()).pathname === '/api/demo/session' && response.request().method() === 'POST' && response.ok()) {
            const value = await response.headerValue('X-English-Roulette-Solo');
            if (/^[A-F0-9]{64}$/.test(value || '')) credentials.set(page, value);
        }
    });
}
function credential(page) { return credentials.get(page); }
async function soloResponse(page) {
    return page.request.get(new URL('/api/demo/session', page.url()).href, {
        headers: credential(page) ? { 'X-English-Roulette-Solo': credential(page) } : {}
    });
}
async function soloState(page) {
    const response = await soloResponse(page);
    assert.ok(response.ok(), await response.text()); return response.json();
}
async function createSolo(page, base, storyVersionId, navigate = true, language = 'en') {
    watchSolo(page);
    if (navigate) await page.goto(base);
    if (language === 'pt-BR') {
        await page.getByRole('button', { name: 'Português', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('.language-selector')?.disabled === false);
    }
    await page.getByRole('button', { name: /Iniciar aventura solo/ }).waitFor();
    const route = async route => route.request().method() === 'POST'
        ? route.continue({ postData: JSON.stringify({ storyVersionId, language }) }) : route.continue();
    await page.route('**/api/demo/session', route);
    try {
        await page.getByRole('button', { name: /Iniciar aventura solo/ }).click();
        await page.locator('.phase-preloading').waitFor({ timeout: 15000 });
        return await soloState(page);
    } finally { await page.unroute('**/api/demo/session', route); }
}
module.exports = { watchSolo, credential, soloResponse, soloState, createSolo };
