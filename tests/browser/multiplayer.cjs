// Real published content and real server clocks; never runs against production.
const assert = require('node:assert/strict');
module.exports = async function multiplayer({ browser, base, errors, screen }) {
    if (!['127.0.0.1', 'localhost'].includes(new URL(base).hostname)) throw Error('Use an isolated local QA server.');
    const people = [];
    for (const width of [390, 1440]) {
        const context = await browser.newContext({ viewport: { width, height: width < 500 ? 844 : 1000 } });
        const page = await context.newPage();
        page.on('pageerror', error => errors.push(error.message));
        page.on('console', message => { if (/Unhandled exception|crit:/.test(message.text())) errors.push(message.text()); });
        people.push({ context, page });
    }
    const [host, guest] = people;
    // One participant has no SignalR: snapshots must still carry the real match.
    await guest.page.route('**/hubs/rooms/**', route => route.abort());
    let failMedia = true;
    await guest.page.route('**/media/catalog/**', route => failMedia ? route.fulfill({ status: 503, body: '' }) : route.continue());
    await guest.page.addInitScript(() => {
        const play = HTMLMediaElement.prototype.play;
        HTMLMediaElement.prototype.play = function () {
            if (document.querySelector('.phase-narrating') && !window.qaBlockedOnce) {
                window.qaBlockedOnce = true;
                return Promise.reject(new DOMException('QA autoplay blocked', 'NotAllowedError'));
            }
            return play.call(this);
        };
    });
    const snapshot = async person => (await person.page.request.get(base + '/api/rooms/current')).json();
    const waitForChatBottom = person => person.page.waitForFunction(() => {
        const log = document.querySelector('.chat-log');
        return log && Math.abs(log.scrollHeight - log.clientHeight - log.scrollTop) <= 2;
    });
    try {
        let badIdentity = true, failedCatalog = true;
        await host.page.route('**/api/catalog', route => failedCatalog ? route.fulfill({ status: 503, body: '' }) : route.continue());
        await host.page.route('**/api/rooms/identity', route => badIdentity ? route.fulfill({ status: 200, contentType: 'application/json', body: '{"id":' }) : route.continue());
        await host.page.goto(base + '/multiplayer');
        await host.page.locator('.admin-error').waitFor();
        failedCatalog = false;
        await host.page.getByRole('button', { name: 'Tentar reconectar', exact: true }).click();
        await host.page.getByLabel('Seu apelido').fill('QAHost');
        await host.page.getByRole('button', { name: 'Entrar no lobby →', exact: true }).click();
        await host.page.getByText('Não foi possível ler a resposta do servidor.', { exact: false }).waitFor();
        assert.ok(await host.page.getByRole('button', { name: 'Entrar no lobby →', exact: true }).isEnabled());
        badIdentity = false;
        await host.page.getByRole('button', { name: 'Entrar no lobby →', exact: true }).click();
        await host.page.getByRole('button', { name: 'Criar sala →', exact: true }).click();
        await host.page.locator('.room-code').waitFor();
        const code = (await host.page.locator('.room-code').innerText()).trim();
        const whatsapp = new URL(await host.page.getByRole('link', { name: 'Enviar convite pelo WhatsApp' }).getAttribute('href'));
        assert.equal(whatsapp.hostname, 'wa.me');
        assert.ok(whatsapp.searchParams.get('text').includes('/join/' + code));
        await host.context.grantPermissions(['clipboard-read', 'clipboard-write']);
        await host.page.getByRole('button', { name: 'Copiar link', exact: true }).click();
        assert.ok((await host.page.evaluate(() => navigator.clipboard.readText())).endsWith('/join/' + code));
        await host.page.getByRole('button', { name: 'Compartilhar', exact: true }).waitFor();
        for (let index = 0; index < 8; index++) {
            await host.page.getByLabel('Mensagem para a sala').fill(`Mensagem ${index} ${'teste de rolagem '.repeat(8)}`);
            await host.page.getByRole('button', { name: 'Enviar', exact: true }).click();
        }
        await host.page.locator('.chat-message').last().getByText('Mensagem 7', { exact: false }).waitFor();
        await waitForChatBottom(host);
        await guest.page.goto(base + '/join/' + code);
        await guest.page.getByLabel('Seu apelido').fill('QAGuest');
        await guest.page.getByRole('button', { name: 'Entrar no lobby →', exact: true }).click();
        await guest.page.getByRole('button', { name: 'Entrar na sala →', exact: true }).click();
        await guest.page.locator('.chat-message').last().getByText('Mensagem 7', { exact: false }).waitFor();
        await waitForChatBottom(guest);
        await guest.page.getByLabel('Mensagem para a sala').fill('Cheguei!');
        await guest.page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await host.page.locator('.chat-message').last().getByText('Cheguei!', { exact: true }).waitFor();
        await waitForChatBottom(host);
        await guest.page.getByRole('button', { name: 'Estou pronto! ✓', exact: true }).click();
        await host.page.locator('.room-member').filter({ hasText: 'QAGuest' }).locator('.member-ready.is-ready').waitFor();
        await host.page.getByRole('button', { name: 'Estou pronto! ✓', exact: true }).click();
        const launch = host.page.getByRole('button', { name: /Iniciar e girar/ });
        await launch.waitFor();
        await host.page.waitForFunction(() => !document.querySelector('.room-start-bar button')?.disabled);
        assert.ok((await launch.boundingBox()).y < 844, 'Host start must be visible on mobile.');
        await screen(host.page, 'multiplayer-ready-mobile');
        await launch.click();
        for (const person of people) await person.page.locator('.phase-preloading').waitFor({ timeout: 20000 });
        await screen(host.page, 'multiplayer-preloading-mobile');
        const audioAction = await host.page.locator('.scene-ready button').boundingBox();
        assert.ok(audioAction && audioAction.y < 844, 'The audio confirmation must be discoverable on mobile.');
        for (const person of people) {
            await person.page.locator('.chat-message').last().getByText('Cheguei!', { exact: true }).waitFor();
            await waitForChatBottom(person);
        }
        console.log('Multiplayer: shared preload.');
        const initial = await snapshot(host), shared = await snapshot(guest);
        assert.equal(initial.storyVersionId, shared.storyVersionId);
        assert.equal(initial.match.id, shared.match.id);
        await guest.page.getByText('Não foi possível carregar a narração.', { exact: true }).waitFor();
        const manualAudio = guest.page.getByRole('button', { name: 'Carregar e ativar áudio', exact: true });
        assert.ok(await manualAudio.isEnabled());
        await manualAudio.click();
        await guest.page.getByText('O navegador não liberou o áudio.', { exact: false }).waitFor();
        assert.equal((await snapshot(guest)).match.myAudioReady, false);
        failMedia = false;
        await guest.page.getByRole('button', { name: 'Tentar carregar áudio novamente', exact: true }).click();
        let questions = 0, votes = 0, refreshed = false;
        for (let scene = 0; scene < 15; scene++) {
            const before = await snapshot(host);
            console.log('Multiplayer scene:', scene, before.match.scene.sceneKey, before.match.scene.phase);
            if (before.match.scene.phase === 'StoryResult') break;
            assert.equal(before.match.scene.phase, 'Preloading');
            for (const person of people) {
                await person.page.waitForFunction(() => !document.querySelector('.scene-ready button')?.disabled);
                await person.page.getByRole('button', { name: 'Ativar áudio e confirmar', exact: true }).click();
                await person.page.waitForFunction(() => document.querySelector('.scene-ready button')?.textContent.includes('confirmado') || !document.querySelector('.scene-ready'));
            }
            for (const person of people) await person.page.locator('.phase-countdown').waitFor();
            const a = await snapshot(host), b = await snapshot(guest);
            assert.equal(a.match.scene.phaseDeadlineUtc, b.match.scene.phaseDeadlineUtc);
            assert.equal(a.match.sceneInstanceId, b.match.sceneInstanceId);
            for (const person of people) await person.page.locator('.phase-narrating').waitFor({ timeout: 10000 });
            if (scene === 0) {
                await guest.page.getByRole('button', { name: 'Reproduzir narração sincronizada', exact: true }).waitFor();
                await guest.page.getByRole('button', { name: 'Reproduzir narração sincronizada', exact: true }).click();
            }
            for (const person of people) {
                await person.page.waitForFunction(() => {
                    const audio = document.querySelector('.audio-player audio');
                    return audio && !audio.paused && audio.currentTime > 0;
                });
                const timing = await snapshot(person);
                const expectedOffset = timing.match.scene.audio.durationMilliseconds / 1000 + 2 -
                    (Date.parse(timing.match.scene.phaseDeadlineUtc) - Date.parse(timing.serverNowUtc)) / 1000;
                const media = await person.page.locator('.audio-player audio').evaluate(audio => ({ position: audio.currentTime, duration: audio.duration }));
                assert.ok(Math.abs(media.position - Math.min(media.duration, expectedOffset)) < 1.5, 'Narration must follow the shared server timeline.');
                assert.equal(await person.page.locator('.audio-player audio').getAttribute('controls'), null);
                assert.ok(await person.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
            }
            if (!refreshed) {
                await screen(host.page, 'multiplayer-narrating-mobile');
                await screen(guest.page, 'multiplayer-narrating-desktop');
                const previous = await snapshot(host);
                await host.page.reload(); await host.page.locator('.phase-narrating').waitFor();
                const restored = await snapshot(host);
                assert.equal(restored.match.id, previous.match.id);
                assert.equal(restored.match.scene.phaseDeadlineUtc, previous.match.scene.phaseDeadlineUtc);
                refreshed = true;
            }
            for (const person of people) await person.page.locator('.phase-questionopen').waitFor({ timeout: 60000 });
            const question = await snapshot(host);
            assert.ok(question.match.scene.question.options.length >= 2);
            assert.equal(question.match.scene.answer, null);
            assert.deepEqual(question.match.roundResults, []);
            let releaseRead, oldReadDelivered;
            if (scene === 0) {
                let captured; const capturedRead = new Promise(resolve => { captured = resolve; });
                const gate = new Promise(resolve => { releaseRead = resolve; });
                let delivered; oldReadDelivered = new Promise(resolve => { delivered = resolve; });
                let held = false;
                await host.page.route('**/api/rooms/current', async route => {
                    if (held) return route.continue();
                    held = true;
                    const response = await route.fetch();
                    captured(); await gate; await route.fulfill({ response }); delivered();
                });
                await Promise.race([capturedRead, host.page.waitForTimeout(5000).then(() => { throw Error("Polling did not request a snapshot during the question."); })]);
            }
            try {
                await host.page.locator('.option').first().click();
                await host.page.getByText('Resposta registrada.', { exact: false }).waitFor({ timeout: 5000 });
            } finally { releaseRead?.(); }
            if (oldReadDelivered) {
                await oldReadDelivered; await host.page.waitForTimeout(200);
                assert.equal(await host.page.locator('.option:enabled').count(), 0, 'A late snapshot cannot undo an accepted answer.');
                await host.page.unroute('**/api/rooms/current');
            }
            const hidden = await snapshot(host);
            assert.equal(hidden.match.scene.answer, null);
            assert.deepEqual(hidden.match.roundResults, []);
            assert.ok(hidden.match.myAnswerAccepted);
            await guest.page.locator('.option').first().click();
            for (const person of people) await person.page.locator('.phase-questionreveal').waitFor();
            questions++;
            await screen(guest.page, 'multiplayer-question-desktop');
            for (const person of people) await person.page.waitForFunction(() => document.querySelector('.phase-decisionopen, .phase-preloading, .phase-storyresult'), null, { timeout: 12000 });
            const next = await snapshot(host);
            if (next.match.scene.phase === 'DecisionOpen') {
                votes++;
                const choices = next.match.scene.choices;
                await host.page.locator('.decision .choice').first().click();
                await host.page.getByText('Voto registrado.', { exact: false }).waitFor();
                await guest.page.locator('.decision .choice').last().click();
                for (const person of people) await person.page.locator('.phase-preloading').waitFor();
                const resolved = await snapshot(host);
                assert.ok(resolved.match.lastDecision.tied);
                assert.ok(choices.some(choice => choice.label === resolved.match.lastDecision.choice));
                assert.equal(resolved.match.scene.sceneKey, (await snapshot(guest)).match.scene.sceneKey);
            }
        }
        for (const person of people) await person.page.locator('.phase-storyresult').waitFor();
        const final = await snapshot(host), same = await snapshot(guest);
        assert.ok(questions > 0); assert.ok(votes > 0);
        assert.ok(final.match.winners.length > 0);
        assert.deepEqual(final.match.players.map(player => player.score), same.match.players.map(player => player.score));
        await host.page.reload(); await host.page.locator('.phase-storyresult').waitFor();
        assert.deepEqual((await snapshot(host)).match.winners, final.match.winners);
        await screen(host.page, 'multiplayer-result-mobile');
        await screen(guest.page, 'multiplayer-result-desktop');
        console.log(JSON.stringify({ realMultiplayerCompleted: true, questions, votes, audioRetry: true, blockedAutoplayRecovery: true, refresh: true, pollingWithoutSignalR: true, slowReadDoesNotBlockAnswers: true, malformedResponseRecovery: true }));
        for (const person of people) await person.page.getByRole('button', { name: 'Sair da sala', exact: true }).click();
    } finally { for (const person of people) await person.context.close(); }
};
if (require.main === module) {
    const { chromium } = require(process.env.ENGLISHROULETTE_PLAYWRIGHT_PATH || 'playwright');
    const fs = require('node:fs'), path = require('node:path');
    let browser; const errors = [];
    (async () => {
        browser = await chromium.launch({ executablePath: '/opt/google/chrome/chrome', headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
        await module.exports({ browser, base: process.env.ENGLISHROULETTE_BASE_URL || 'http://127.0.0.1:5087', errors,
            screen: async (page, name) => { const output = process.env.ENGLISHROULETTE_SCREENSHOTS; if (output) { fs.mkdirSync(output, { recursive: true }); await page.screenshot({ path: path.join(output, name + '.png'), fullPage: true }); } } });
        assert.deepEqual(errors, []);
    })().catch(error => { console.error(error); console.error(errors); process.exitCode = 1; }).finally(async () => browser?.close());
}
