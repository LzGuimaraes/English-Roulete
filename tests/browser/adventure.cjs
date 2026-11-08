// Run against a local QA instance with published, narrated content. See GAME_EXPERIENCE.md.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.ENGLISHROULETTE_PLAYWRIGHT_PATH || 'playwright');
const base = process.env.ENGLISHROULETTE_BASE_URL || 'http://127.0.0.1:5087';
const output = process.env.ENGLISHROULETTE_SCREENSHOTS;
if (!['127.0.0.1', 'localhost'].includes(new URL(base).hostname)) throw Error('Use an isolated local QA server.');
const errors = [];
let browser;
async function screen(page, name) {
    if (output) { fs.mkdirSync(output, { recursive: true }); await page.screenshot({ path: path.join(output, name + '.png'), fullPage: true }); }
}
async function open(width = 1440) {
    const context = await browser.newContext({ viewport: { width, height: width < 500 ? 844 : 1000 } });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (/Unhandled exception|crit:/.test(message.text())) errors.push(message.text()); });
    return { context, page };
}
async function home(page) {
    await page.goto(base);
    await page.getByRole('button', { name: /Iniciar aventura solo/ }).waitFor();
}
async function ready(page) {
    await page.getByRole('button', { name: /Ouvir e iniciar cena/ }).waitFor();
    await page.waitForFunction(() => !document.querySelector('.scene-ready button')?.disabled, null, { timeout: 15000 });
    await page.waitForFunction(() => [...document.querySelectorAll('.scene-backdrop img')].some(image => image.complete && image.naturalWidth > 0));
}
async function start(page) {
    console.log("Starting a real adventure:", page.viewportSize().width);
    const [response] = await Promise.all([
        page.waitForResponse(r => r.url().endsWith('/api/demo/session') && r.request().method() === 'POST'),
        page.getByRole('button', { name: /Iniciar aventura solo|Tentar iniciar novamente/ }).click()
    ]);
    const state = await response.json();
    assert.ok(state.storyVersionId);
    await page.locator('.in-adventure').waitFor({ timeout: 10000 });
    assert.equal(await page.locator('.adventure-heading h1').innerText(), state.title);
    await page.waitForFunction(() => document.activeElement === document.querySelector('.adventure-heading h1'));
    return state;
}
async function progressiveText(page) {
    const snapshot = await (await page.request.get(base + '/api/demo/session')).json();
    const copy = page.locator('.narration-copy');
    await page.waitForFunction(() => document.querySelector('.narration-copy')?.textContent.length > 0);
    assert.ok((await copy.textContent()).length < snapshot.narration.length);
    const player = await page.locator('.audio-player').boundingBox();
    assert.ok(player.width <= 335 && player.height <= 70, 'The player must remain compact.');
    await page.locator('audio').evaluate(audio => audio.pause());
    const pausedText = await copy.textContent();
    await page.waitForTimeout(1100);
    assert.equal(await copy.textContent(), pausedText, 'Paused audio must freeze text.');
    const paused = await (await page.request.get(base + '/api/demo/session')).json();
    assert.equal(paused.phaseDeadlineUtc, snapshot.phaseDeadlineUtc, 'Pausing must not change the server deadline.');
    await page.locator('audio').evaluate(audio => { audio.currentTime = audio.duration / 2; });
    await page.waitForFunction(() => {
        const count = document.querySelector('.narration-copy').textContent.length;
        const total = document.querySelector('.narration-layout').textContent.length;
        return count > total * .45 && count < total * .6;
    });
    await page.getByRole('button', { name: 'Mostrar texto completo', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.narration-copy').textContent === document.querySelector('.narration-layout').textContent);
    await page.getByRole('button', { name: 'Acompanhar narração', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.narration-copy').textContent.length < document.querySelector('.narration-layout').textContent.length);
    await page.reload();
    await page.locator('.narration-copy').waitFor();
    await page.waitForFunction(() => {
        const audio = document.querySelector('audio');
        const total = document.querySelector('.narration-layout').textContent.length;
        const count = document.querySelector('.narration-copy').textContent.length;
        return audio.currentTime > 0 && Math.abs(count - Math.floor(total * audio.currentTime / audio.duration)) <= 3;
    });
    await page.getByRole('button', { name: 'Mostrar texto completo', exact: true }).click();
    await page.getByRole('button', { name: 'Acompanhar narração', exact: true }).click();
    await page.locator('audio').evaluate(audio => audio.play());
    console.log('PASS: progressive text follows audio pause, seek, resume, refresh and full-text preference.');
}
async function completeAdventure(page) {
    let decisions = 0, questions = 0;
    for (let step = 0; step < 8; step++) {
        if (await page.locator('.ending').count()) break;
        await ready(page);
        await page.getByRole('button', { name: /Ouvir e iniciar cena/ }).click();
        await page.locator('.narration').waitFor();
        assert.ok(await page.locator('audio').evaluate(audio => !audio.paused));
        if (step === 0) await progressiveText(page);
        await screen(page, `scene-${step}`);
        await page.waitForFunction(() => !!document.querySelector('.question-panel .option:not(:disabled)'), null, { timeout: 90000 });
        await page.locator('.question-panel .option').first().click();
        await page.locator('.feedback').waitFor(); questions++;
        if (step === 0) {
            await page.reload(); await page.locator('.feedback').waitFor();
            assert.ok(await page.locator('.journey-stop').count());
        }
        await page.getByRole('button', { name: /Continuar a história/ }).click();
        await page.waitForFunction(() => document.querySelector('.decision .choice, .scene-ready button, .ending'));
        if (await page.locator('.decision .choice').count()) { decisions++; await page.locator('.decision .choice').first().click(); }
        if (await page.locator('.ending').count()) break;
    }
    assert.ok(await page.locator('.ending').isVisible());
    const ending = await (await page.request.get(base + '/api/demo/session')).json();
    assert.equal(await page.locator('.ending .narration').innerText(), ending.narration);
    assert.equal(await page.locator('.ending-audio').getAttribute('src'), ending.audio.url);
    await page.locator('.ending-audio').evaluate(audio => audio.play());
    assert.ok(await page.locator('.ending-audio').evaluate(audio => !audio.paused));
    await screen(page, 'ending');

    assert.ok(questions >= 1);
    console.log(JSON.stringify({ completedRealAdventure: true, questions, decisions }));
}
async function failedStart(body, status) {
    const { context, page } = await open(390);
    let fail = true;
    await page.route('**/api/demo/session', route => route.request().method() === 'POST' && fail
        ? route.fulfill({ status, contentType: 'application/json', body }) : route.continue());
    await home(page);
    await page.getByRole('button', { name: /Iniciar aventura solo/ }).click();
    await page.locator('.launch-error').waitFor();
    assert.equal(await page.locator('.in-adventure').count(), 0);
    assert.equal(await page.locator('.story-wheel.has-result').count(), 0);
    assert.ok(await page.getByRole('button', { name: /Tentar iniciar novamente/ }).isEnabled());
    await screen(page, `error-${status}`);
    fail = false; await start(page);
    await context.close();
}
(async () => {
    browser = await chromium.launch({ executablePath: process.env.ENGLISHROULETTE_CHROME || '/opt/google/chrome/chrome',
        headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
    if (!process.env.ENGLISHROULETTE_ONLY_MULTIPLAYER) {
    for (const width of [1440, 390]) {
        console.log("QA viewport:", width);
        const { context, page } = await open(width);
        await home(page);
        await page.locator('.round-sample blockquote').waitFor();
        const preview = await (await context.request.get(base + '/api/catalog/round-preview')).json();
        assert.equal(await page.locator('.round-sample blockquote').innerText(), preview.narrationExcerpt);
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await screen(page, `home-${width}`);
        await page.getByRole('button', { name: /Como jogar/ }).click();
        await page.getByRole('dialog').waitFor(); await page.keyboard.press('Escape');
        await start(page); await ready(page);
        await screen(page, `preloading-${width}`);
        assert.equal(await page.locator('.journey-card').getAttribute('open'), null);
        await page.locator('.journey-card summary').click();
        assert.ok(await page.locator('.journey-stop').first().isVisible());
        await page.locator('.journey-card summary').click();
        if (width === 1440) await completeAdventure(page);
        else {
            await page.reload(); await ready(page);
            assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        }
        await page.getByRole('button', { name: 'Menu principal', exact: true }).click();
        await start(page); // Player disposal and a new adventure must also work.
        await context.close();
    }
    await failedStart('{"title":', 200);
    await failedStart(JSON.stringify({ detail: 'CONTENT_NOT_PUBLISHED' }), 409);
    await failedStart(JSON.stringify({ title: 'Temporarily unavailable' }), 503);
    {
        const { context, page } = await open(390);
        await page.route('**/api/catalog', route => route.fulfill({ json: [] }));
        await page.route('**/api/catalog/round-preview', route => route.fulfill({ status: 204 }));
        await home(page);
        assert.ok(await page.getByRole('button', { name: /Iniciar aventura solo/ }).isDisabled());
        assert.ok(await page.getByText('Ainda não há histórias publicadas para jogar.', { exact: false }).isVisible());
        assert.equal(await page.locator('.wheel-label').count(), 0); // No fabricated genres.
        await page.getByRole('button', { name: 'Atualizar catálogo', exact: true }).click();
        await context.close();
    }
    {
        const { context, page } = await open(); let fail = true;
        await page.route('**/api/catalog', route => fail ? route.fulfill({ status: 503, json: {} }) : route.continue());
        await home(page);
        await page.getByRole('button', { name: 'Tentar carregar histórias', exact: true }).waitFor();
        fail = false; await page.getByRole('button', { name: 'Tentar carregar histórias', exact: true }).click();
        await start(page); await context.close();
    }
    {
        const { context, page } = await open();
        await page.route('**/api/demo/session', async route => {
            if (route.request().method() !== 'POST') return route.continue();
            await page.waitForTimeout(18000).catch(() => {});
            await route.abort().catch(() => {});
        });
        await home(page); await page.getByRole('button', { name: /Iniciar aventura solo/ }).click();
        await page.getByText('O servidor demorou para responder.', { exact: false }).waitFor({ timeout: 20000 });
        assert.ok(await page.getByRole('button', { name: /Tentar iniciar novamente/ }).isEnabled());
        await context.close();
    }
    {
        const { context, page } = await open(390);
        await page.route('**/game-atmosphere.js', route => route.abort());
        await home(page); await start(page); // An optional wheel scroll cannot block the adventure.
        await context.close();
    }
    {
        const { context, page } = await open(390);
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await home(page); await start(page); // No animationend event is required.
        if (await page.locator('.scene-backdrop .is-animated').count()) {
            assert.equal(await page.locator('.scene-backdrop .is-animated').evaluate(element => getComputedStyle(element).display), 'none');
        }
        await ready(page);
        await page.getByRole('button', { name: /Ouvir e iniciar cena/ }).click();
        await page.waitForFunction(() => {
            const copy = document.querySelector('.narration-copy'), layout = document.querySelector('.narration-layout');
            return copy && layout && copy.textContent === layout.textContent;
        });
        await context.close();
    }
    {
        const { context, page } = await open(390);
        await page.route('**/media/visuals/**', route => route.fulfill({ status: 404, body: '' }));
        await home(page); await start(page);
        await page.getByText('A imagem não carregou. Você pode continuar pelo texto e áudio.', { exact: true }).waitFor();
        await page.waitForFunction(() => !document.querySelector('.scene-ready button')?.disabled);
        await page.getByRole('button', { name: /Ouvir e iniciar cena/ }).click();
        await page.waitForFunction(() => !!document.querySelector('audio') && !document.querySelector('audio').paused);
        await context.close();
    }
    }
    await require('./multiplayer.cjs')({ browser, base, errors, screen });
    assert.deepEqual(errors, []);
    console.log(process.env.ENGLISHROULETTE_ONLY_MULTIPLAYER ? 'PASS: multiplayer checks completed.' : 'PASS: automatic solo start, real story completion, recovery, timeout, catalog, preview, resume, desktop and mobile.');
})().catch(error => { console.error(error); console.error("Browser errors:", errors); process.exitCode = 1; }).finally(async () => { await browser?.close(); });
