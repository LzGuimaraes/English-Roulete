const assert = require('node:assert/strict'), fs = require('node:fs');
const { chromium } = require(process.env.ENGLISHROULETTE_PLAYWRIGHT_PATH || 'playwright');
const { createSolo, soloState } = require('./solo-page.cjs');
const base = process.env.ENGLISHROULETTE_BASE_URL || 'http://127.0.0.1:5087';
if (!['localhost', '127.0.0.1'].includes(new URL(base).hostname)) throw Error('Use isolated local QA.');
const shots = process.env.ENGLISHROULETTE_SCREENSHOTS || '.local/portuguese-shots';
const errors = [];
fs.mkdirSync(shots, { recursive: true });
async function startAudio(page) {
    await page.waitForFunction(() => document.querySelector('.scene-ready button')?.disabled === false);
    await page.getByRole('button', { name: /Ouvir e iniciar cena/ }).click();
    await page.locator('.phase-narrating').waitFor();
}
async function useDefinition(page) {
    await page.getByRole('button', { name: 'Mostrar diálogo completo', exact: true }).click();
    const word = page.locator('.narration-hint:visible').first();
    assert.match(await word.getAttribute('aria-label'), /Ver significado de/);
    await word.click(); await page.locator('.inline-hint-translation').waitFor();
    await page.getByRole('button', { name: 'Fechar dica', exact: true }).click();
    await page.getByRole('button', { name: 'Acompanhar diálogo', exact: true }).click();
}
(async () => {
    const browser = await chromium.launch({ executablePath: '/opt/google/chrome/chrome', headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
    try {
        await Promise.all([1366, 390].map(async width => {
            const context = await browser.newContext({ viewport: { width, height: width === 390 ? 844 : 900 } }), page = await context.newPage();
            page.on('pageerror', e => errors.push(e.message));
            try {
                const english = await (await context.request.get(base + '/api/catalog')).json();
                const portuguese = await (await context.request.get(base + '/api/catalog?language=pt-BR')).json();
                assert.ok(portuguese.length >= 2); assert.ok(portuguese.every(s => s.language === 'pt-BR'));
                assert.ok(english.every(s => s.language === 'en' && !portuguese.some(p => p.id === s.id)));
                const story = portuguese.find(s => s.title === 'O Rolo Azul'); assert.ok(story);
                await page.goto(base); await page.getByRole('button', { name: 'Português', exact: true }).click();
                await page.waitForFunction(() => document.querySelector('.language-selector')?.disabled === false);
                await page.locator('.round-sample blockquote[lang="pt-BR"]').waitFor();
                assert.equal(await page.getByRole('button', { name: 'Português', exact: true }).getAttribute('aria-pressed'), 'true');
                assert.equal(await page.getByText(/O nível indica a complexidade do inglês/).count(), 0);
                assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
                await page.screenshot({ path: `${shots}/home-${width}.png`, fullPage: true });
                let state = await createSolo(page, base, story.id, false, 'pt-BR');
                assert.equal(state.language, 'pt-BR'); assert.equal(state.score.total, 0);
                await startAudio(page); await useDefinition(page);
                assert.equal((await soloState(page)).hintCost.usedCount, 1);
                assert.ok(await page.locator('.audio-player audio').evaluate(a => !a.paused));
                await page.screenshot({ path: `${shots}/solo-${width}.png`, fullPage: true });
                await page.locator('.phase-questionopen').waitFor({ timeout: 70000 }); state = await soloState(page);
                const source = JSON.parse(fs.readFileSync('content/portuguese-01/the-blue-reel-a1-pt-br/story.json'));
                const key = source.scenes.find(s => s.key === state.sceneKey).correctOptionKey.toUpperCase();
                await page.locator('.option').filter({ has: page.locator('.option-letter', { hasText: key }) }).click();
                await page.locator('.phase-questionreveal').waitFor(); state = await soloState(page);
                assert.equal(state.score.total, 135); assert.ok(state.answer.isCorrect);
                await page.getByRole('button', { name: 'Menu principal', exact: true }).click();
                await page.getByRole('button', { name: 'Inglês', exact: true }).click();
                await page.locator('.round-sample blockquote[lang="en"]').waitFor();
                assert.equal(await page.getByRole('button', { name: 'Inglês', exact: true }).getAttribute('aria-pressed'), 'true');
                await page.route('**/api/catalog?language=pt-BR', route => route.fulfill({ status: 503, json: {} }));
                await page.getByRole('button', { name: 'Português', exact: true }).click();
                await page.getByRole('button', { name: 'Tentar carregar histórias', exact: true }).waitFor();
                assert.ok(await page.getByRole('button', { name: 'Iniciar aventura solo', exact: false }).isDisabled());
                await page.unroute('**/api/catalog?language=pt-BR');
                await page.getByRole('button', { name: 'Tentar carregar histórias', exact: true }).click();
                await page.waitForFunction(() => !document.querySelector('.start-button')?.disabled);
                await page.getByRole('button', { name: 'Inglês', exact: true }).click();
                await page.waitForFunction(() => !document.querySelector('.language-selector')?.disabled);
                await page.route('**/api/catalog?language=pt-BR', route => route.fulfill({ json: [] }));
                await page.getByRole('button', { name: 'Português', exact: true }).click();
                await page.getByText(/Ainda não há histórias publicadas em português/).waitFor();
                assert.equal(await page.locator('.wheel-label').count(), 0);
                console.log('PASS Portuguese solo, paid definition, scoring, switch, error and empty state', width);
            } finally { await context.close(); }
        }));
        const hostContext = await browser.newContext({ viewport: { width: 1366, height: 900 } });
        const guestContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
        const host = await hostContext.newPage(), guest = await guestContext.newPage();
        for (const page of [host, guest]) page.on('pageerror', e => errors.push(e.message));
        try {
            const stamp = Date.now().toString().slice(-6);
            await host.goto(base + '/multiplayer');
            await host.getByLabel('Seu apelido', { exact: true }).fill('Lusohost' + stamp);
            await host.getByRole('button', { name: /Entrar no lobby/ }).click();
            await host.getByRole('button', { name: 'Português', exact: true }).click();
            await host.getByRole('button', { name: /Criar sala/ }).click();
            const code = await host.locator('.room-code').innerText();
            await guest.goto(base + '/join/' + code);
            await guest.getByLabel('Seu apelido', { exact: true }).fill('Lusoguest' + stamp);
            await guest.getByRole('button', { name: /Entrar no lobby/ }).click();
            await guest.getByRole('button', { name: /Entrar na sala/ }).click();
            await guest.locator('.room-heading').getByText(/Português/).waitFor();
            await host.getByRole('button', { name: /Estou pronto/ }).click();
            await guest.getByRole('button', { name: /Estou pronto/ }).click();
            await host.getByRole('button', { name: /Iniciar e girar a roleta/ }).click();
            for (const page of [host, guest]) {
                await page.locator('.phase-preloading').waitFor({ timeout: 20000 });
                await page.waitForFunction(() => !document.querySelector('.scene-ready button')?.disabled);
                await page.getByRole('button', { name: 'Ativar áudio e confirmar', exact: true }).click();
            }
            await Promise.all([host, guest].map(page => page.locator('.phase-narrating').waitFor({ timeout: 15000 })));
            await useDefinition(host);
            const hostRoom = await (await hostContext.request.get(base + '/api/rooms/current')).json();
            const guestRoom = await (await guestContext.request.get(base + '/api/rooms/current')).json();
            assert.equal(hostRoom.language, 'pt-BR'); assert.equal(guestRoom.language, 'pt-BR');
            assert.equal(hostRoom.storyVersionId, guestRoom.storyVersionId);
            assert.equal(hostRoom.match.scene.hintCost.usedCount, 1); assert.equal(guestRoom.match.scene.hintCost.usedCount, 0);
            for (const [page, width] of [[host,1366],[guest,390]]) {
                assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
                await page.screenshot({ path: `${shots}/group-${width}.png`, fullPage: true });
                await page.getByRole('button', { name: 'Sair da sala', exact: true }).click();
            }
            console.log('PASS Portuguese room, shared roulette, guest adoption, audio synchronization and private paid definition');
        } finally { await hostContext.close(); await guestContext.close(); }
        assert.deepEqual(errors, []);
        console.log('PASS language browser checks without JavaScript errors');
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
