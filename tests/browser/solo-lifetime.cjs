const assert = require('node:assert/strict'), fs = require('node:fs');
const { chromium } = require(process.env.ENGLISHROULETTE_PLAYWRIGHT_PATH || 'playwright');
const { createSolo, soloState, soloResponse, credential, watchSolo } = require('./solo-page.cjs');
const base = process.env.ENGLISHROULETTE_BASE_URL || 'http://127.0.0.1:5087';
if (!['127.0.0.1', 'localhost'].includes(new URL(base).hostname)) throw Error('Use isolated local QA.');
const errors = [];
let browser;
async function menu(page) {
    await page.getByRole('button', { name: /Iniciar aventura solo/ }).waitFor();
    assert.equal(await page.locator('.in-adventure').count(), 0);
    assert.equal((await soloResponse(page)).status(), 404);
}
async function start(page) {
    await page.waitForFunction(() => document.querySelector('.scene-ready button')?.disabled === false);
    await page.getByRole('button', { name: /Ouvir e iniciar cena/ }).click();
    await page.locator('.phase-narrating').waitFor();
}
async function hint(page) {
    await page.getByRole('button', { name: /Mostrar (diálogo|texto) completo/ }).click();
    await page.locator('.narration-hint:visible').first().click();
    await page.locator('.inline-hint-translation').waitFor();
    assert.equal((await soloState(page)).hintCost.usedCount, 1);
    await page.getByRole('button', { name: 'Fechar tradução', exact: true }).click();
}
function fresh(state) {
    assert.equal(state.phase, 'Preloading'); assert.equal(state.sceneNumber, 1);
    assert.equal(state.score.total, 0); assert.equal(state.hintCost.usedCount, 0);
    assert.ok(state.wordHints.every(word => word.translation === null));
}
async function noStorage(page, context, secret) {
    assert.match(secret, /^[A-F0-9]{64}$/);
    assert.ok(!(await context.cookies()).some(cookie => cookie.name === 'englishroulette.demo'));
    const stored = await page.evaluate(() => JSON.stringify([Object.entries(localStorage), Object.entries(sessionStorage)]));
    assert.ok(!stored.includes(secret));
}
(async () => {
    browser = await chromium.launch({ executablePath: '/opt/google/chrome/chrome', headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
    for (const width of [1366, 390]) {
        const context = await browser.newContext({ viewport: { width, height: width < 768 ? 844 : 900 } });
        let page = await context.newPage(); watchSolo(page);
        page.on('pageerror', error => errors.push(error.message));
        const catalog = await (await context.request.get(base + '/api/catalog')).json();
        const story = catalog.find(story => story.title.startsWith('A Visitor Made of Light'));
        assert.ok(story);
        let state = await createSolo(page, base, story.id); fresh(state);
        const initial = state.sceneInstanceId;
        await start(page); await hint(page);
        if (width === 1366) {
            await page.locator('.phase-questionopen').waitFor({ timeout: 70000 });
            state = await soloState(page);
            const source = JSON.parse(fs.readFileSync('content/anthology-01/a-visitor-made-of-light-a2/story.json'));
            const correct = source.scenes.find(scene => scene.key === state.sceneKey).correctOptionKey.toUpperCase();
            await page.locator('.option').filter({ has: page.locator('.option-letter', { hasText: correct }) }).click();
            await page.locator('.phase-questionreveal').waitFor();
            assert.ok((await soloState(page)).score.total > 0);
            await page.getByRole('button', { name: /Continuar a história/ }).click();
            await page.locator('.phase-preloading').waitFor();
            assert.equal((await soloState(page)).sceneNumber, 2);
        }
        let old = credential(page);
        await noStorage(page, context, old);
        await page.reload(); await menu(page);
        state = await createSolo(page, base, story.id); fresh(state); assert.notEqual(state.sceneInstanceId, initial);
        await start(page); await hint(page);
        old = credential(page);
        await page.getByRole('button', { name: 'Menu principal', exact: true }).click(); await menu(page);
        assert.equal((await context.request.get(base + '/api/demo/session', { headers: { 'X-English-Roulette-Solo': old } })).status(), 404);

        await createSolo(page, base, story.id);
        await page.locator('.brand').click(); await menu(page);
        await page.goto(base + '/multiplayer');
        await page.getByRole('link', { name: /Solo/ }).click(); await menu(page);
        await createSolo(page, base, story.id, false);
        old = credential(page);
        await page.goBack(); await page.waitForURL('**/multiplayer');
        await page.waitForFunction(() => !document.querySelector('[data-solo-page]'));
        await page.goForward(); await menu(page);
        assert.equal((await context.request.get(base + '/api/demo/session', { headers: { 'X-English-Roulette-Solo': old } })).status(), 404);

        await createSolo(page, base, story.id);
        await page.goto(base + '/health/live'); await page.goBack(); await menu(page);
        await createSolo(page, base, story.id);
        await page.close();
        page = await context.newPage(); watchSolo(page); page.on('pageerror', error => errors.push(error.message));
        await page.goto(base); await menu(page);

        // Two tabs share browser cookies but never a solo capability.
        await createSolo(page, base, story.id); const firstTab = (await soloState(page)).sceneInstanceId;
        const second = await context.newPage(); watchSolo(second);
        await second.goto(base); await menu(second);
        await createSolo(second, base, story.id);
        assert.notEqual((await soloState(second)).sceneInstanceId, firstTab);
        await second.getByRole('button', { name: 'Menu principal', exact: true }).click(); await menu(second);
        assert.equal((await soloState(page)).sceneInstanceId, firstTab);
        await second.close();
        await page.getByRole('button', { name: 'Menu principal', exact: true }).click(); await menu(page);

        // Migrate an old persistent cookie: it cannot resume and is expired on entry.
        const response = await context.request.post(base + '/api/demo/session', { headers: { 'X-English-Roulette': 'demo' }, data: { storyVersionId: story.id } });
        assert.ok(response.ok()); const legacy = response.headers()['x-english-roulette-solo'];
        await context.addCookies([{ name: 'englishroulette.demo', value: legacy, domain: new URL(base).hostname, path: '/api/demo', httpOnly: true, sameSite: 'Strict' }]);
        await page.reload(); await menu(page); await noStorage(page, context, legacy);
        assert.equal((await context.request.get(base + '/api/demo/session', { headers: { 'X-English-Roulette-Solo': legacy } })).status(), 404);
        console.log(`PASS ${width}px: reload, menu, logo, SPA exit/back, full-document back, close/reopen, independent tabs and legacy-cookie cleanup.`);
        await context.close();
    }
    assert.deepEqual(errors, []); console.log('PASS: every solo return starts fresh; no browser errors.');
})().catch(error => { console.error(error); console.error(errors); process.exitCode = 1; }).finally(async () => browser?.close());
