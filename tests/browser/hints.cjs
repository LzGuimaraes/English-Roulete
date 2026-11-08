// Real media and server timelines in isolated QA. No mocked questions or scores.
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { chromium } = require(process.env.ENGLISHROULETTE_PLAYWRIGHT_PATH || 'playwright');
const base = process.env.ENGLISHROULETTE_BASE_URL || 'http://127.0.0.1:5087';
if (!['127.0.0.1', 'localhost'].includes(new URL(base).hostname)) throw Error('Use isolated local QA.');
const output = process.env.ENGLISHROULETTE_SCREENSHOTS || '.local/hint-shots';
fs.mkdirSync(output, { recursive: true });
const sources = fs.readdirSync('content').flatMap(pack => fs.readdirSync(`content/${pack}`, { withFileTypes: true })
    .filter(entry => entry.isDirectory() && fs.existsSync(`content/${pack}/${entry.name}/story.json`))
    .map(entry => JSON.parse(fs.readFileSync(`content/${pack}/${entry.name}/story.json`))));
const errors = [];
let browser;
async function open(width) {
    const context = await browser.newContext({ viewport: { width, height: width < 768 ? 844 : 900 } });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (/Unhandled exception|crit:/.test(message.text())) errors.push(message.text()); });
    return { context, page };
}
async function json(response) { assert.ok(response.ok(), await response.text()); return response.json(); }
async function soloState(person) { return json(await person.context.request.get(base + '/api/demo/session')); }
async function roomState(person) { return json(await person.context.request.get(base + '/api/rooms/current')); }
async function post(person, route, data) {
    const csrf = await json(await person.context.request.get(base + '/api/rooms/antiforgery'));
    const response = await person.context.request.post(base + route, { headers: { 'X-CSRF-TOKEN': csrf.token }, data });
    assert.ok(response.ok(), await response.text()); return response.status() === 204 ? null : response.json();
}
async function send(person, kind, key) {
    const room = await roomState(person);
    return post(person, `/api/rooms/${room.id}/commands`, { requestId: crypto.randomUUID(),
        expectedStateVersion: room.match.scene.stateVersion, sceneInstanceId: room.match.sceneInstanceId, kind, key });
}
function correct(state) {
    const source = sources.find(source => source.metadata.title === state.title);
    assert.ok(source, 'Use the reviewed anthology QA catalog.');
    return source.scenes.find(scene => scene.key === state.sceneKey).correctOptionKey;
}
function fresh(state) {
    assert.ok(state.wordHints.length > 0, 'A published QA scene needs real editorial words.');
    assert.equal(state.hintCost.usedCount, 0);
    assert.ok(state.wordHints.every(word => word.translation === null));
}
async function fullText(page) {
    const toggle = page.getByRole('button', { name: /Mostrar (diálogo|texto) completo/ });
    if (await toggle.count()) await toggle.click();
    else if (await page.locator('.scene-review summary').count()) await page.locator('.scene-review summary').click();
}
async function buy(person, state, getState, name, showFull = true) {
    const { page } = person, word = state.wordHints[0];
    if (showFull) await fullText(page);
    const button = page.locator(`.narration-hint[data-hint-key="${word.key}"]:visible`).first();
    assert.match(await button.getAttribute('aria-label'), /Traduzir/);
    await button.focus(); await page.keyboard.press('Enter');
    await page.locator('.inline-hint-translation').waitFor();
    let updated = await getState(person);
    const scene = updated.match?.scene || updated;
    assert.equal(scene.hintCost.usedCount, 1);
    const translation = scene.wordHints.find(hint => hint.key === word.key).translation;
    assert.ok(translation);
    assert.equal(await page.locator('.inline-hint-translation').innerText(), translation);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    await page.screenshot({ path: path.join(output, `${name}.png`), fullPage: true });
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => !document.querySelector('.inline-hint-card'));
    assert.equal(await button.evaluate(button => document.activeElement === button), true);
    // A second click opens the receipt locally; no second charge is allowed.
    await button.click(); await page.locator('.inline-hint-translation').waitFor();
    updated = await getState(person);
    assert.equal((updated.match?.scene || updated).hintCost.usedCount, 1);
    await page.getByRole('button', { name: 'Fechar tradução', exact: true }).click();
    return updated;
}
async function startSolo(person) {
    await person.page.getByRole('button', { name: /Ouvir e iniciar cena/ }).waitFor();
    await person.page.waitForFunction(() => !document.querySelector('.scene-ready button').disabled);
    await person.page.getByRole('button', { name: /Ouvir e iniciar cena/ }).click();
    await person.page.locator('.phase-narrating').waitFor();
}
async function solo() {
    const person = await open(1366), { context, page } = person;
    try {
        const catalog = await json(await context.request.get(base + '/api/catalog'));
        const story = catalog.find(story => story.title.startsWith('A Visitor Made of Light'));
        assert.ok(story, 'Import the reviewed dialogue story into QA.');
        const create = async () => json(await context.request.post(base + '/api/demo/session', {
            headers: { 'X-English-Roulette': 'demo' }, data: { storyVersionId: story.id } }));
        let state = await create(); fresh(state);
        const first = state.sceneInstanceId;
        await page.goto(base);
        for (let scene = 0; scene < 8; scene++) {
            await startSolo(person); state = await soloState(person); fresh(state);
            if (scene === 0) {
                // Exercise a live, progressively revealed word before switching to review.
                const cue = state.dialogue.find(cue => cue.text.toLowerCase().includes(state.wordHints[0].term.toLowerCase()));
                assert.ok(cue);
                const audio = page.locator('.audio-player audio');
                await audio.evaluate((audio, time) => { audio.pause(); audio.currentTime = time; }, (cue.endMilliseconds - 5) / 1000);
                await page.waitForFunction(key => [...document.querySelectorAll('.dialogue-line .narration-hint')]
                    .some(button => button.dataset.hintKey === key && !button.inert && button.getClientRects().length), state.wordHints[0].key);
                await buy(person, state, soloState, 'solo-desktop', false);
                await audio.evaluate(audio => audio.play());
                await page.setViewportSize({ width: 390, height: 844 });
                await page.reload(); await page.locator('.phase-narrating').waitFor();
                state = await soloState(person); assert.equal(state.hintCost.usedCount, 1);
                await fullText(page);
                await page.locator(`.narration-hint[data-hint-key="${state.wordHints[0].key}"]:visible`).first().click();
                await page.locator('.inline-hint-translation').waitFor();
                await page.screenshot({ path: path.join(output, 'solo-mobile-reload.png'), fullPage: true });
                await page.getByRole('button', { name: 'Fechar tradução', exact: true }).click();
            }
            await page.locator('.phase-questionopen').waitFor({ timeout: 70000 });
            state = await soloState(person);
            await page.locator('.option').filter({ has: page.locator('.option-letter', { hasText: correct(state).toUpperCase() }) }).click();
            await page.locator('.phase-questionreveal').waitFor();
            state = await soloState(person);
            assert.equal(state.score.roundDeduction, scene === 0 ? Math.ceil(state.score.roundBase / 10) : 0);
            await page.getByRole('button', { name: /Continuar a história/ }).click();
            await page.waitForFunction(() => document.querySelector('.phase-preloading,.phase-decisionopen,.phase-storyresult'));
            if (await page.locator('.phase-decisionopen').count()) {
                await page.locator('.choice').first().click();
                await page.waitForFunction(() => document.querySelector('.phase-preloading,.phase-storyresult'));
            }
            if (await page.locator('.phase-storyresult').count()) break;
        }
        await page.locator('.phase-storyresult').waitFor();
        state = await soloState(person); assert.ok(state.score.total > 0);
        // Replay the exact same story and same editorial keys in the same browser.
        state = await create(); fresh(state); assert.notEqual(state.sceneInstanceId, first); assert.equal(state.score.total, 0);
        await page.reload(); await startSolo(person);
        state = await soloState(person); fresh(state);
        await buy(person, state, soloState, 'solo-second-game');
        await page.locator('.phase-questionopen').waitFor({ timeout: 70000 }); state = await soloState(person);
        await page.locator('.option').filter({ has: page.locator('.option-letter', { hasText: correct(state).toUpperCase() }) }).click();
        await page.locator('.phase-questionreveal').waitFor(); state = await soloState(person);
        assert.equal(state.score.total, state.score.roundPoints); assert.ok(state.score.roundDeduction > 0);
        console.log('PASS: solo click/keyboard/reopen/reload, per-scene reset, completed story and new game with fresh paid hint/score.');
    } finally { await context.close(); }
}
async function readyAudio(person) {
    await person.page.waitForFunction(() => document.querySelector('.scene-ready button')?.disabled === false);
    await person.page.getByRole('button', { name: 'Ativar áudio e confirmar', exact: true }).click();
}
async function recovery() {
    const person = await open(390), { context, page } = person;
    try {
        const catalog = await json(await context.request.get(base + '/api/catalog'));
        const story = catalog.find(story => story.title.startsWith('A Visitor Made of Light'));
        assert.ok(story);
        await json(await context.request.post(base + '/api/demo/session', {
            headers: { 'X-English-Roulette': 'demo' }, data: { storyVersionId: story.id } }));
        await page.goto(base); await startSolo(person); await fullText(page);
        let reject = true;
        await page.route('**/api/demo/commands', async route => {
            if (reject && route.request().postDataJSON()?.kind === 'hint') {
                reject = false;
                await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({
                    title: 'Serviço temporariamente indisponível', detail: 'Tente novamente.', status: 503 }) });
            } else await route.continue();
        });
        await page.locator('.narration-hint:visible').first().click();
        await page.getByRole('button', { name: 'Tentar traduzir novamente', exact: true }).waitFor();
        assert.equal((await soloState(person)).hintCost.usedCount, 0);
        assert.equal(await page.locator('.inline-hint-translation').count(), 0);
        await page.getByRole('button', { name: 'Tentar traduzir novamente', exact: true }).click();
        await page.locator('.inline-hint-translation').waitFor();
        assert.equal((await soloState(person)).hintCost.usedCount, 1);
        console.log('PASS: failed HTTP purchase exposes retry without a translation or charge; real retry buys exactly once.');
    } finally { await context.close(); }
}
async function multiplayer() {
    const host = await open(1366), guest = await open(390), people = [host, guest];
    try {
        for (const [index, person] of people.entries()) await json(await person.context.request.post(base + '/api/rooms/identity', {
            headers: { 'X-English-Roulette': 'demo' }, data: { nickname: `HintTester${index + 1}`, avatar: '🦊' } }));
        let room = await post(host, '/api/rooms/', { capacity: 2 });
        room = await post(guest, '/api/rooms/join', { code: room.code });
        const spin = async () => {
            for (const person of people) room = await post(person, `/api/rooms/${room.id}/ready`, { expectedStateVersion: room.stateVersion, ready: true });
            await host.page.goto(base + '/multiplayer');
            await host.page.getByRole('button', { name: /Iniciar e girar/ }).click();
            await host.page.locator('.phase-preloading').waitFor({ timeout: 15000 });
            await guest.page.goto(base + '/multiplayer'); await guest.page.locator('.phase-preloading').waitFor();
        };
        await spin(); let firstRound;
        for (let scene = 0; scene < 8; scene++) {
            for (const person of people) await readyAudio(person);
            await host.page.locator('.phase-narrating').waitFor({ timeout: 12000 });
            room = await roomState(host); fresh(room.match.scene);
            if (scene === 0) {
                firstRound = room.match.sceneInstanceId;
                await buy(host, room.match.scene, roomState, 'group-host-first-game');
                assert.ok((await roomState(guest)).match.scene.wordHints.every(word => word.translation === null));
                await host.page.reload(); await host.page.locator('.phase-narrating').waitFor();
                assert.equal((await roomState(host)).match.scene.hintCost.usedCount, 1);
            }
            await host.page.locator('.phase-questionopen').waitFor({ timeout: 70000 }); room = await roomState(host);
            const key = correct(room.match.scene);
            await send(host, 'answer', key); await host.page.waitForTimeout(380); await send(guest, 'answer', key);
            await host.page.locator('.phase-questionreveal').waitFor(); room = await roomState(host);
            const mine = room.match.roundResults.find(result => result.playerId === room.viewerId);
            const other = room.match.roundResults.find(result => result.playerId !== room.viewerId);
            assert.equal(mine.points, scene === 0 ? 180 : 200); assert.equal(other.points, 180);
            await host.page.waitForFunction(() => document.querySelector('.phase-preloading,.phase-decisionopen,.phase-storyresult'), null, { timeout: 15000 });
            room = await roomState(host);
            if (room.match.scene.phase === 'DecisionOpen') for (const person of people) await send(person, 'vote', room.match.scene.choices[0].key);
            await host.page.waitForFunction(() => document.querySelector('.phase-preloading,.phase-storyresult'));
            if (await host.page.locator('.phase-storyresult').count()) break;
        }
        await host.page.locator('.match-podium').waitFor(); room = await roomState(host);
        const previous = room;
        await host.page.getByRole('button', { name: /Jogar outra história/ }).click();
        await host.page.getByRole('heading', { name: 'Vamos para a partida 2?', exact: true }).waitFor();
        room = await roomState(host); assert.equal(room.match, null); assert.equal(room.gameNumber, 2);
        await spin();
        for (const person of people) await readyAudio(person);
        await host.page.locator('.phase-narrating').waitFor({ timeout: 12000 });
        room = await roomState(host); fresh(room.match.scene); fresh((await roomState(guest)).match.scene);
        assert.notEqual(room.match.sceneInstanceId, firstRound);
        await buy(guest, (await roomState(guest)).match.scene, roomState, 'group-guest-second-game');
        assert.equal((await roomState(host)).match.scene.hintCost.usedCount, 0);
        await host.page.locator('.phase-questionopen').waitFor({ timeout: 70000 }); room = await roomState(host);
        const key = correct(room.match.scene);
        await send(host, 'answer', key); await host.page.waitForTimeout(380); await send(guest, 'answer', key);
        await host.page.locator('.phase-questionreveal').waitFor(); room = await roomState(host);
        assert.equal(room.match.roundResults.find(result => result.playerId === room.viewerId).points, 200);
        assert.equal(room.match.roundResults.find(result => result.playerId !== room.viewerId).points, 162);
        for (const player of room.match.players) {
            const old = previous.match.players.find(old => old.id === player.id);
            assert.equal(player.score, old.score + (player.id === room.viewerId ? 200 : 162));
        }
        console.log('PASS: private group hint/reconnect, complete game, podium, rematch retaining totals and charging only the new buyer.');
    } finally {
        for (const person of people) {
            try { const response = await person.context.request.get(base + '/api/rooms/current'); if (response.ok()) await post(person, `/api/rooms/${(await response.json()).id}/leave`, {}); } catch { }
            await person.context.close();
        }
    }
}
(async () => {
    browser = await chromium.launch({ executablePath: '/opt/google/chrome/chrome', headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
    const mode = process.env.ENGLISHROULETTE_FEATURE_MODE;
    if (mode === 'errors') await recovery();
    else await Promise.all([...(mode === 'multiplayer' ? [] : [solo()]), ...(mode === 'solo' ? [] : [multiplayer()])]);
    assert.deepEqual(errors, []); console.log('PASS: inline hints across solo and group games, no browser errors.');
})().catch(error => { console.error(error); console.error(errors); process.exitCode = 1; }).finally(async () => browser?.close());
