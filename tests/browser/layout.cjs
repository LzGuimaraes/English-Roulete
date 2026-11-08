// Real API states and published content. No fabricated questions, scores or deadlines.
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { chromium } = require(process.env.ENGLISHROULETTE_PLAYWRIGHT_PATH || 'playwright');
const base = process.env.ENGLISHROULETTE_BASE_URL || 'http://127.0.0.1:5087';
if (!['127.0.0.1', 'localhost'].includes(new URL(base).hostname)) throw Error('Use local QA.');
const output = process.env.ENGLISHROULETTE_SCREENSHOTS || '.local/responsive-shots';
fs.mkdirSync(output, { recursive:true });
const sizes = [[1920,1080],[1366,768],[1280,720],[1024,768],[1024,600],[768,1024],[800,600],[390,844],[320,568]];
const results = [], errors = [], failures = [];
let browser;
async function check(page, state) {
    for (const [width,height] of sizes) {
        await page.setViewportSize({width,height});
        await page.waitForTimeout(60);
        const row = await page.evaluate(() => {
            const root=document.documentElement;
            const controls=[...document.querySelectorAll('.story-card button, .scoreboard-toggle')].filter(e=>e.getClientRects().length && !e.disabled);
            return {width:innerWidth,height:innerHeight,pageHeight:root.scrollHeight,pageWidth:root.scrollWidth,
                controls:controls.map(e=>({text:e.textContent.trim(),bottom:e.getBoundingClientRect().bottom,right:e.getBoundingClientRect().right})),
                headingBottom:document.querySelector('.adventure-heading')?.getBoundingClientRect().bottom,
                scoreboardTop:document.querySelector('.match-scoreboard')?.getBoundingClientRect().top};
        });
        row.state=state; results.push(row);
        if (row.pageWidth>width+1) failures.push(`${state} ${width}x${height}: horizontal ${row.pageWidth}`);
        if (width>=768 && row.pageHeight>height+1) failures.push(`${state} ${width}x${height}: vertical ${row.pageHeight}`);
        if (width>=768 && row.scoreboardTop !== undefined && row.headingBottom !== undefined && row.scoreboardTop<row.headingBottom-1) failures.push(`${state} ${width}x${height}: scoreboard overlaps heading`);
        if (width>=768 && row.controls.some(c=>c.bottom>height+1 || c.right>width+1)) failures.push(`${state} ${width}x${height}: action outside viewport`);
        if ([1366,1024,800,390].includes(width)) await page.screenshot({path:path.join(output,`${state}-${width}x${height}.png`),fullPage:true});
    }
    await page.setViewportSize({width:1366,height:768});
    console.log('Layout checked:',state);
}
async function open() {
    const context=await browser.newContext({viewport:{width:1366,height:768}});
    const page=await context.newPage();
    page.on('pageerror',e=>errors.push(e.message));
    page.on('console',m=>{if(/Unhandled exception|crit:/.test(m.text())) errors.push(m.text());});
    return {context,page};
}
async function solo() {
    const {context,page}=await open();
    const catalog=await (await context.request.get(base+'/api/catalog')).json();
    const story=catalog.find(s=>s.difficulty==='B1') || catalog[0];
    const create=await context.request.post(base+'/api/demo/session',{headers:{'X-English-Roulette':'demo'},data:{storyVersionId:story.id}});
    assert.ok(create.ok());
    await page.goto(base); await page.locator('.phase-preloading').waitFor();
    await check(page,'solo-preloading');
    for(let scene=0;scene<15;scene++) {
        await page.waitForFunction(()=>!document.querySelector('.scene-ready button')?.disabled);
        await page.getByRole('button',{name:/Ouvir e iniciar cena/}).click();
        await page.locator('.phase-narrating').waitFor();
        if(scene===0) {
            await check(page,'solo-narrating');
            const timer=page.locator('.game-countdown .countdown-number').first();
            const before=await timer.innerText();
            await page.waitForTimeout(1100);
            assert.notEqual(await timer.innerText(),before);
            await page.emulateMedia({reducedMotion:'reduce'});
            assert.equal(await timer.evaluate(e=>getComputedStyle(e).animationName),'none');
            await page.emulateMedia({reducedMotion:'no-preference'});
        }
        await page.locator('.phase-questionopen').waitFor({timeout:70000});
        if(scene===0) await check(page,'solo-question');
        await page.locator('.option').first().click(); await page.locator('.phase-questionreveal').waitFor();
        if(scene===0) {
            await check(page,'solo-feedback');
            await page.locator('.scene-review summary').click();
            assert.ok(await page.locator('.scene-review-content').isVisible());
            await page.locator('.scene-review summary').click();
        }
        await page.getByRole('button',{name:/Continuar a história/}).click();
        await page.waitForFunction(()=>document.querySelector('.phase-decisionopen, .phase-preloading, .phase-storyresult'));
        if(await page.locator('.phase-decisionopen').count()) {
            await check(page,'solo-decision'); await page.locator('.choice').first().click();
        }
        if(await page.locator('.phase-storyresult').count()) break;
    }
    assert.ok(await page.locator('.phase-storyresult').count());
    await check(page,'solo-ending');
    await context.close();
}
async function multiplayer() {
    const people=[]; let heartbeat;
    const snap=async p=>(await p.context.request.get(base+'/api/rooms/current')).json();
    const post=async(p,url,data)=>{
        const csrf=await (await p.context.request.get(base+'/api/rooms/antiforgery')).json();
        const response=await p.context.request.post(base+url,{headers:{'X-CSRF-TOKEN':csrf.token},data});
        assert.ok(response.ok(),await response.text()); return response.status()===204?null:response.json();
    };
    try {
        for(let i=0;i<8;i++) {
            const person=i<2?await open():{context:await browser.newContext()};
            const response=await person.context.request.post(base+'/api/rooms/identity',{headers:{'X-English-Roulette':'demo'},data:{nickname:`Explorador-Numero-${i+1}`,avatar:'🦊'}});
            assert.ok(response.ok()); people.push(person);
        }
        const [host,guest]=people;
        let room=await post(host,'/api/rooms/',{capacity:8});
        for(const person of people.slice(1)) room=await post(person,'/api/rooms/join',{code:room.code});
        for(const person of people) room=await post(person,`/api/rooms/${room.id}/ready`,{expectedStateVersion:room.stateVersion,ready:true});
        heartbeat=setInterval(()=>{for(const p of people.slice(2)) p.context.request.get(base+'/api/rooms/current').catch(()=>{});},5000);
        await host.page.goto(base+'/multiplayer'); await host.page.locator('.room-code').waitFor();
        await check(host.page,'lobby-eight');
        await host.page.getByRole('button',{name:/Iniciar e girar/}).click();
        await host.page.locator('.phase-preloading').waitFor({timeout:15000});
        await guest.page.goto(base+'/multiplayer'); await guest.page.locator('.phase-preloading').waitFor();
        const send=async(person,kind,key)=>{const current=await snap(person);return post(person,`/api/rooms/${current.id}/commands`,{requestId:crypto.randomUUID(),expectedStateVersion:current.match.scene.stateVersion,sceneInstanceId:current.match.sceneInstanceId,kind,key});};
        for(let scene=0;scene<15;scene++) {
            if(scene===0) await check(host.page,'multi-preloading-eight');
            if(process.env.ENGLISHROULETTE_LAYOUT_PHASE==='preloading') return;
            room=await snap(host);
            for(const person of people.slice(2)) {
                const media=await person.context.request.get(base+room.match.scene.audio.url);assert.ok(media.ok());
                await send(person,'audio-ready');
            }
            await guest.page.waitForFunction(()=>!document.querySelector('.scene-ready button')?.disabled);
            await guest.page.getByRole('button',{name:'Ativar áudio e confirmar',exact:true}).click();
            await host.page.waitForFunction(()=>!document.querySelector('.scene-ready button')?.disabled);
            await host.page.getByRole('button',{name:'Ativar áudio e confirmar',exact:true}).click();
            if(scene===0) {
                await host.page.locator('.phase-countdown').waitFor();
                await host.page.screenshot({path:path.join(output,'cartoon-countdown.png')});
                assert.ok(await host.page.locator('.countdown-large').isVisible());
            }
            await host.page.locator('.phase-narrating').waitFor({timeout:12000});
            if(scene===0) await check(host.page,'multi-narrating-eight');
            await host.page.locator('.phase-questionopen').waitFor({timeout:70000});
            if(scene===0) await check(host.page,'multi-question-eight');
            room=await snap(host);
            for(const person of people) await send(person,'answer',room.match.scene.question.options[0].key);
            await host.page.locator('.phase-questionreveal').waitFor();
            if(scene===0) await check(host.page,'multi-feedback-eight');
            await host.page.waitForFunction(()=>document.querySelector('.phase-decisionopen, .phase-preloading, .phase-storyresult'),null,{timeout:12000});
            room=await snap(host);
            if(room.match.scene.phase==='DecisionOpen') {
                await check(host.page,'multi-decision-eight');
                for(const person of people) await send(person,'vote',room.match.scene.choices[0].key);
            }
            if(room.match.scene.phase==='StoryResult') break;
            await host.page.waitForFunction(()=>document.querySelector('.phase-preloading, .phase-storyresult'));
            if(await host.page.locator('.phase-storyresult').count()) break;
        }
        assert.ok(await host.page.locator('.phase-storyresult').count());
        await check(host.page,'multi-ending-eight');
    } finally {
        clearInterval(heartbeat);
        for(const p of people) {
            try {const current=await snap(p);if(current) await post(p,`/api/rooms/${current.id}/leave`,{});}catch{}
            await p.context.close();
        }
    }
}
(async()=>{
    browser=await chromium.launch({executablePath:process.env.ENGLISHROULETTE_CHROME||'/opt/google/chrome/chrome',headless:true,args:['--no-sandbox','--disable-dev-shm-usage']});
    const selected=process.env.ENGLISHROULETTE_LAYOUT_MODE;
    await Promise.all([...(selected==='multiplayer'?[]:[solo()]),...(selected==='solo'?[]:[multiplayer()])]);
    fs.writeFileSync(path.join(output,'measurements.json'),JSON.stringify({results,failures,errors},null,2));
    assert.deepEqual(errors,[]);assert.deepEqual(failures,[]);
    console.log(`PASS: ${results.length} phase/viewport combinations; mode=${selected || "all"}; phase=${process.env.ENGLISHROULETTE_LAYOUT_PHASE || "all"}.`);
})().catch(error=>{fs.writeFileSync(path.join(output,'measurements.json'),JSON.stringify({results,failures,errors},null,2));console.error(error);console.error(failures);process.exitCode=1;}).finally(async()=>browser?.close());
