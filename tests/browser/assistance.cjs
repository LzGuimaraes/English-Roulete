// Real published content, media clocks and server-owned scores. Local QA only.
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { chromium } = require(process.env.ENGLISHROULETTE_PLAYWRIGHT_PATH || 'playwright');
const base = process.env.ENGLISHROULETTE_BASE_URL || 'http://127.0.0.1:5087';
if (!['127.0.0.1', 'localhost'].includes(new URL(base).hostname)) throw Error('Use isolated local QA.');
const output = process.env.ENGLISHROULETTE_SCREENSHOTS || '.local/assistance-shots';
fs.mkdirSync(output, { recursive: true });
const errors = [], measurements = [], failures = [];
const sizes = [[1920,1080],[1366,768],[1280,720],[1024,768],[1024,600],[768,1024],[800,600],[390,844],[320,568]];
const sources = fs.readdirSync('content/anthology-01').filter(slug => fs.existsSync(`content/anthology-01/${slug}/story.json`))
    .map(slug => JSON.parse(fs.readFileSync(`content/anthology-01/${slug}/story.json`)));
let browser;
async function open() {
    const context = await browser.newContext({ viewport: {width:1440,height:900} });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (/Unhandled exception|crit:/.test(message.text())) errors.push(message.text()); });
    return {context,page};
}
async function check(page, name) {
    for (const [width,height] of sizes) {
        await page.setViewportSize({width,height}); await page.waitForTimeout(60);
        const row = await page.evaluate(() => ({width:innerWidth,height:innerHeight,documentWidth:document.documentElement.scrollWidth,
            documentHeight:document.documentElement.scrollHeight,
            controls:[...document.querySelectorAll('.story-card button, .room-start-bar button, .room-ready, .room-heading button, .room-invitation button')].filter(e=>e.getClientRects().length && !e.disabled)
                .map(e=>({text:e.textContent.trim(),bottom:e.getBoundingClientRect().bottom,right:e.getBoundingClientRect().right}))}));
        row.name=name; measurements.push(row);
        if(row.documentWidth > width+1) failures.push(`${name}: horizontal ${width}x${height}`);
        if (width>=768) {
            if(row.documentHeight > height+1) failures.push(`${name}: vertical ${width}x${height}: ${row.documentHeight}`);
            for (const control of row.controls) if(control.bottom>height+1 || control.right>width+1) failures.push(`${name}: offscreen ${control.text} ${width}x${height}`);
        }
        if ([390,1366,1024,800].includes(width)) await page.screenshot({path:path.join(output,`${name}-${width}.png`),fullPage:true});
    }
    await page.setViewportSize({width:1440,height:900});
}
async function post(person,url,data) {
    const csrf=await (await person.context.request.get(base+'/api/rooms/antiforgery')).json();
    const response=await person.context.request.post(base+url,{headers:{'X-CSRF-TOKEN':csrf.token},data});
    assert.ok(response.ok(),await response.text());return response.status()===204?null:response.json();
}
async function snapshot(person) { const r=await person.context.request.get(base+'/api/rooms/current');return r.ok()?r.json():null; }
async function send(person,kind,key) {const room=await snapshot(person);return post(person,`/api/rooms/${room.id}/commands`,
    {requestId:crypto.randomUUID(),expectedStateVersion:room.match.scene.stateVersion,sceneInstanceId:room.match.sceneInstanceId,kind,key});}
async function solo() {
    const person=await open(), {context,page}=person;
    try {
        const catalog=await (await context.request.get(base+'/api/catalog')).json();
        const story=catalog.find(story=>story.title.startsWith('A Visitor Made of Light'));
        assert.ok(story,'Import the reviewed dialogue story into QA.');
        const response=await context.request.post(base+'/api/demo/session',{headers:{'X-English-Roulette':'demo'},data:{storyVersionId:story.id}});
        assert.ok(response.ok()); const initial=await response.json();
        assert.ok(initial.dialogue.length>1); assert.ok(initial.wordHints.length>0);
        assert.ok(initial.wordHints.every(h=>h.translation===null));
        await page.goto(base); await page.getByRole('button',{name:/Ouvir e iniciar cena/}).waitFor();
        await page.waitForFunction(()=>!document.querySelector('.scene-ready button').disabled);
        await page.getByRole('button',{name:/Ouvir e iniciar cena/}).click();
        await page.locator('.dialogue-bubble').waitFor();
        await page.locator('.word-hints summary').click();
        await page.locator('.hint-word').first().click();
        await page.locator('.hint-translation').waitFor();
        const used=await (await context.request.get(base+'/api/demo/session')).json();
        assert.equal(used.hintCost.usedCount,1); assert.ok(used.wordHints[0].translation);
        await page.locator('.word-hints summary').click();
        const audio=page.locator('.audio-player audio'); await audio.evaluate(a=>a.pause());
        await page.waitForTimeout(150);const paused=await page.locator('.dialogue-line').textContent();
        await page.waitForTimeout(250);assert.equal(await page.locator('.dialogue-line').textContent(),paused);
        const cue=used.dialogue[1];
        await audio.evaluate((a,time)=>{a.currentTime=time;},(cue.startMilliseconds+cue.endMilliseconds)/2000);
        await page.waitForFunction(speaker=>document.querySelector('.dialogue-speaker')?.textContent===speaker,cue.speaker);
        assert.ok((await page.locator('.dialogue-line').textContent()).length>0);
        await page.getByRole('button',{name:'Mostrar diálogo completo',exact:true}).click();
        assert.equal(await page.locator('.dialogue-review strong').count(),used.dialogue.length);
        await page.getByRole('button',{name:'Acompanhar diálogo',exact:true}).click();
        await page.emulateMedia({reducedMotion:'reduce'});
        await page.waitForFunction(text=>document.querySelector('.dialogue-line')?.textContent===text,cue.text);
        await page.emulateMedia({reducedMotion:'no-preference'});
        await check(page,'dialogue');
        await audio.evaluate(a=>a.play());
        await page.locator('.phase-questionopen').waitFor({timeout:60000});
        const authored=sources.find(s=>s.metadata.title===used.title).scenes.find(s=>s.key===used.sceneKey);
        await page.locator('.option').filter({has:page.locator('.option-letter',{hasText:authored.correctOptionKey.toUpperCase()})}).click();
        await page.locator('.phase-questionreveal').waitFor();
        const scored=await (await context.request.get(base+'/api/demo/session')).json();
        assert.ok(scored.score.roundDeduction>0);assert.equal(scored.score.total,scored.score.roundPoints);
        await check(page,'solo-score');
        await page.reload();await page.locator('.phase-questionreveal').waitFor();
        assert.equal((await (await context.request.get(base+'/api/demo/session')).json()).score.total,scored.score.total);
        console.log('PASS: dialogue pause/seek, full text, reduced motion, paid hint, solo score and reload.');
    } finally {await context.close();}
}
async function multiplayer() {
    const people=[];let heartbeat;
    try {
        for(let index=0;index<8;index++) {
            const person=index<2?await open():{context:await browser.newContext()};people.push(person);
            const response=await person.context.request.post(base+'/api/rooms/identity',{headers:{'X-English-Roulette':'demo'},data:{nickname:`Jogador-Numero-${index+1}`,avatar:'🦊'}});
            assert.ok(response.ok());
        }
        const [host,guest]=people;let room=await post(host,'/api/rooms/',{capacity:8});
        for(const person of people.slice(1)) room=await post(person,'/api/rooms/join',{code:room.code});
        for(const person of people) room=await post(person,`/api/rooms/${room.id}/ready`,{expectedStateVersion:room.stateVersion,ready:true});
        heartbeat=setInterval(()=>{for(const person of people.slice(2)) person.context.request.get(base+'/api/rooms/current').catch(()=>{});},5000);
        await host.page.goto(base+'/multiplayer');await host.page.getByRole('button',{name:/Iniciar e girar/}).click();
        await host.page.locator('.phase-preloading').waitFor({timeout:15000});
        await guest.page.goto(base+'/multiplayer');await guest.page.locator('.phase-preloading').waitFor();
        for(let scene=0;scene<15;scene++) {
            room=await snapshot(host);
            for(const person of people.slice(2)) {
                assert.ok((await person.context.request.get(base+room.match.scene.audio.url)).ok());await send(person,'audio-ready');
            }
            for(const person of [guest,host]) {
                await person.page.waitForFunction(()=>!document.querySelector('.scene-ready button')?.disabled);
                await person.page.getByRole('button',{name:'Ativar áudio e confirmar',exact:true}).click();
            }
            await host.page.locator('.phase-narrating').waitFor({timeout:12000});
            room=await snapshot(host);
            if(scene===0 && room.match.scene.wordHints.length) {
                await host.page.locator('.word-hints summary').click();await host.page.locator('.hint-word').first().click();
                await host.page.locator('.hint-translation').waitFor();await host.page.locator('.word-hints summary').click();
                assert.ok((await snapshot(guest)).match.scene.wordHints.every(h=>h.translation===null));
            }
            await host.page.locator('.phase-questionopen').waitFor({timeout:70000});room=await snapshot(host);
            const source=sources.find(s=>s.metadata.title===room.match.scene.title);
            assert.ok(source,'Use the anthology QA catalog.');
            const key=source.scenes.find(s=>s.key===room.match.scene.sceneKey).correctOptionKey;
            for(const person of people) {await send(person,'answer',key);await host.page.waitForTimeout(380);}
            await host.page.locator('.phase-questionreveal').waitFor();
            await host.page.waitForFunction(()=>document.querySelector('.phase-decisionopen,.phase-preloading,.phase-storyresult'),null,{timeout:12000});
            room=await snapshot(host);
            if(room.match.scene.phase==='DecisionOpen') for(const person of people) await send(person,'vote',room.match.scene.choices[0].key);
            await host.page.waitForFunction(()=>document.querySelector('.phase-preloading,.phase-storyresult'));
            if(await host.page.locator('.phase-storyresult').count()) break;
        }
        await host.page.locator('.match-podium').waitFor();await guest.page.locator('.match-podium').waitFor();
        room=await snapshot(host);assert.equal(room.match.standings.length,8);assert.ok(room.match.standings.some(s=>s.position>3));
        await check(host.page,'podium-eight');
        await host.page.reload();await host.page.locator('.match-podium').waitFor();
        assert.deepEqual((await snapshot(host)).match.standings,room.match.standings);
        if(process.env.ENGLISHROULETTE_AUDIT_PODIUM) {
            await host.page.evaluate(()=>history.replaceState(null,'','/multiplayer#qa-podium-host'));
            console.log('PODIUM_READY: CDP 9223, target qa-podium-host; create .local/resume-podium-audit to continue.');
            const deadline=Date.now()+120000;
            while(!fs.existsSync('.local/resume-podium-audit') && Date.now()<deadline) await host.page.waitForTimeout(500);
        }
        await guest.page.getByRole('button',{name:'Sair e voltar ao menu',exact:true}).click();
        await guest.page.getByRole('button',{name:/Iniciar aventura solo/}).waitFor();
        assert.equal(await snapshot(guest),null);
        await host.page.getByRole('button',{name:'Jogar outra história',exact:false}).click();
        await host.page.getByRole('heading',{name:'Vamos para a partida 2?'}).waitFor();
        let next=await snapshot(host);assert.equal(next.id,room.id);assert.equal(next.code,room.code);assert.equal(next.gameNumber,2);
        assert.equal(next.match,null);assert.ok(next.members.every(m=>!m.ready));assert.equal(next.members.length,7);
        for(const member of next.members) assert.equal(member.score,room.match.players.find(p=>p.id===member.playerId).score);
        if(process.env.ENGLISHROULETTE_HOLD_NEXT_LOBBY) {
            console.log('NEXT_LOBBY_READY: create .local/resume-next-lobby-audit to continue.');
            const deadline=Date.now()+300000;
            while(!fs.existsSync('.local/resume-next-lobby-audit') && Date.now()<deadline) await host.page.waitForTimeout(500);
            await host.page.reload();await host.page.locator('.room-ready').waitFor();
        }
        await check(host.page,'next-lobby');
        for(const person of [host,...people.slice(2)]) next=await post(person,`/api/rooms/${next.id}/ready`,{expectedStateVersion:next.stateVersion,ready:true});
        await host.page.waitForFunction(()=>!document.querySelector('.room-start-bar button.primary')?.disabled);
        await host.page.getByRole('button',{name:/Iniciar e girar/}).click();await host.page.locator('.phase-preloading').waitFor({timeout:15000});
        next=await snapshot(host);assert.notEqual(next.match.id,room.match.id);
        assert.ok(next.match.players.every(p=>p.score===room.match.players.find(old=>old.id===p.id).score));
        assert.ok(next.match.players.every(p=>!p.answered && !p.voted && !p.audioReady));
        assert.equal(next.match.scene.hintCost.usedCount,0);
        console.log('PASS: real eight-player podium, standings, exit to menu, fresh match in same room with accumulated points.');
    } finally {
        clearInterval(heartbeat);
        for(const person of people) {try {const room=await snapshot(person);if(room) await post(person,`/api/rooms/${room.id}/leave`,{});}catch{}await person.context.close();}
    }
}
(async()=>{
    browser=await chromium.launch({executablePath:'/opt/google/chrome/chrome',headless:true,args:['--no-sandbox','--disable-dev-shm-usage',...(process.env.ENGLISHROULETTE_AUDIT_PODIUM?['--remote-debugging-port=9223','--remote-debugging-address=127.0.0.1']:[])]});
    const mode=process.env.ENGLISHROULETTE_FEATURE_MODE;
    await Promise.all([...(mode==='multiplayer'?[]:[solo()]),...(mode==='solo'?[]:[multiplayer()])]);
    assert.deepEqual(errors,[]);assert.deepEqual(failures,[]);
    fs.writeFileSync(path.join(output,'measurements.json'),JSON.stringify({measurements,failures,errors},null,2));
    console.log(`PASS: ${measurements.length} feature/viewport combinations.`);
})().catch(error=>{fs.writeFileSync(path.join(output,'measurements.json'),JSON.stringify({measurements,failures,errors},null,2));console.error(error);console.error(errors);process.exitCode=1;}).finally(async()=>browser?.close());
