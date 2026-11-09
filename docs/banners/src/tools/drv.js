// Interactive capture driver for the Vigil banners: one long-lived Chromium, named browser contexts, driven over HTTP
// so a whole capture session (load the sample day once, then navigate, switch theme, resize) keeps one store and one
// clock. Start: NODE_PATH=<playwright> node drv.js <port>. Then POST JSON commands to http://127.0.0.1:<port>/:
//   {op:"open", ctx, url, w, h, dpr, scheme, clock?}   new context + page (colorScheme light|dark), waits for first paint
//   {op:"click", ctx, x, y} {op:"move", ctx, x, y} {op:"wheel", ctx, dx, dy, x, y} {op:"key", ctx, key} {op:"type", ctx, text}
//   {op:"dpr", ctx, dpr}  change the device pixel ratio in place (CDP), e.g. 2 to drive, 3 for a macro shot
//   {op:"scheme", ctx, scheme}  {op:"viewport", ctx, w, h}  {op:"wait", ms}
//   {op:"shot", ctx, path, clip?, css?}   PNG at the context's DPR (css:true for a 1x peek)
//   {op:"fixtime", ctx, at?|plus?}  hold the page's Date at one moment (needs open with clock:true)
//   {op:"realtime", ctx}  let the page's Date run again
//   {op:"console", ctx, grep?, n?}  recent console lines
//   {op:"cshot", ctx, path, clip?}  PNG at the page's current DPR (use after a 'dpr' switch)
//   {op:"eval", ctx, js}  {op:"close", ctx}  {op:"quit"}
const { chromium } = require('playwright');
const http = require('http');
const port = +process.argv[2] || 8871;
const ctxs = {};
const HOLD_DATE = `(() => {
  const RealDate = Date; let held = null;
  function HeldDate(...a) {
    if (!(this instanceof HeldDate)) return new RealDate(held ?? RealDate.now()).toString();
    return a.length ? new RealDate(...a) : new RealDate(held ?? RealDate.now());
  }
  HeldDate.now = () => held ?? RealDate.now();
  HeldDate.parse = RealDate.parse; HeldDate.UTC = RealDate.UTC; HeldDate.prototype = RealDate.prototype;
  globalThis.Date = HeldDate;
  globalThis.__holdDate = { fix: t => { held = t; }, release: () => { held = null; } };
})();`;
let browser;
async function run(c) {
  const s = ctxs[c.ctx];
  switch (c.op) {
    case 'open': {
      const ctx = await browser.newContext({ viewport: { width: c.w, height: c.h }, deviceScaleFactor: c.dpr || 2, colorScheme: c.scheme || 'light' });
      const p = await ctx.newPage();
      const errors = [];
      const logs = [];
      p.on('pageerror', e => errors.push(String(e)));
      p.on('console', m => { logs.push(m.type() + ': ' + m.text()); if (logs.length > 400) logs.shift(); });
      ctxs[c.ctx] = { ctx, p, errors, logs };
      // clock:true wraps Date before the app boots (it keeps real time until 'fixtime'), so a whole capture session
      // can later be held at one moment and every frame shows the same time. Only Date is wrapped: the runtime's
      // timers and performance.now() stay native. (Playwright's own page.clock fakes every timer and stalls Uno.)
      if (c.clock) await p.addInitScript(HOLD_DATE);
      await p.goto(c.url, { waitUntil: 'load', timeout: 180000 });
      const t0 = Date.now();
      // Uno's Skia canvas paints after the runtime starts: wait until a strip of the window holds real pixels
      while (Date.now() - t0 < 300000) {
        let strip = null;
        try { strip = await p.screenshot({ clip: { x: 0, y: 0, width: c.w, height: Math.min(200, c.h) }, scale: 'css', timeout: 120000 }); } catch (e) {}
        if (strip && strip.length > 6000) break;
        await p.waitForTimeout(2000);
      }
      await p.mouse.move(2, Math.round(c.h / 2));
      return { rendered: Math.round((Date.now() - t0) / 1000) };
    }
    case 'click': await s.p.mouse.move(c.x, c.y); await s.p.mouse.down(); await s.p.waitForTimeout(80); await s.p.mouse.up();
      if (c.park !== false) { await s.p.waitForTimeout(150); const v = s.p.viewportSize(); await s.p.mouse.move(2, Math.round(v.height / 2)); } return {};
    case 'move': await s.p.mouse.move(c.x, c.y); return {};
    case 'wheel': await s.p.mouse.move(c.x, c.y); await s.p.mouse.wheel(c.dx || 0, c.dy || 0); return {};
    case 'park': { const v = s.p.viewportSize(); await s.p.mouse.move(2, Math.round(v.height / 2)); return {}; }
    case 'key': await s.p.keyboard.press(c.key); return {};
    case 'type': await s.p.keyboard.type(c.text, { delay: 40 }); return {};
    case 'scheme': await s.p.emulateMedia({ colorScheme: c.scheme }); return {};
    case 'viewport': await s.p.setViewportSize({ width: c.w, height: c.h });
      if (s.dpr) { s.cdp = s.cdp || await s.ctx.newCDPSession(s.p); await s.cdp.send('Emulation.setDeviceMetricsOverride', { width: c.w, height: c.h, deviceScaleFactor: s.dpr, mobile: false }); }
      return {};
    case 'dpr': { // switch the device pixel ratio in place (same page, same store, same clock)
      const v = s.p.viewportSize(); s.dpr = c.dpr; s.cdp = s.cdp || await s.ctx.newCDPSession(s.p);
      await s.cdp.send('Emulation.setDeviceMetricsOverride', { width: v.width, height: v.height, deviceScaleFactor: c.dpr, mobile: false });
      return { dpr: await s.p.evaluate('devicePixelRatio') }; }
    case 'wait': await new Promise(r => setTimeout(r, c.ms)); return {};
    case 'cshot': { // a CDP capture at the page's current device pixel ratio (honours a 'dpr' switch)
      s.cdp = s.cdp || await s.ctx.newCDPSession(s.p);
      const v = s.p.viewportSize(); const clip = c.clip || { x: 0, y: 0, width: v.width, height: v.height };
      const r = await s.cdp.send('Page.captureScreenshot', { format: 'png', clip: { ...clip, scale: 1 } });
      require('fs').writeFileSync(c.path, Buffer.from(r.data, 'base64')); return { path: c.path }; }
    case 'shot': await s.p.screenshot({ path: c.path, clip: c.clip, scale: c.css ? 'css' : 'device', animations: 'disabled', timeout: 180000 }); return { path: c.path };
    case 'fixtime': { // hold Date at one moment (timers keep running): at = epoch ms, or now + (plus || 0)
      const t = c.at || (Date.now() + (c.plus || 0)); await s.p.evaluate(t => globalThis.__holdDate.fix(t), t);
      return { fixed: new Date(t).toISOString(), page: await s.p.evaluate('new Date().toISOString()') }; }
    case 'realtime': await s.p.evaluate(() => globalThis.__holdDate.release()); return { now: await s.p.evaluate('new Date().toISOString()') };
    case 'eval': return { value: await s.p.evaluate(c.js) };
    case 'errors': return { errors: s.errors };
    case 'console': { const out = s.logs.filter(l => !c.grep || new RegExp(c.grep, 'i').test(l)).slice(-(c.n || 40)); return { logs: out }; }
    case 'close': await s.ctx.close(); delete ctxs[c.ctx]; return {};
    case 'quit': setTimeout(async () => { await browser.close(); process.exit(0); }, 50); return {};
  }
  throw new Error('unknown op ' + c.op);
}
(async () => {
  browser = await chromium.launch({ args: ['--hide-scrollbars'] });
  http.createServer((req, res) => {
    let body = '';
    req.on('data', d => body += d);
    req.on('end', async () => {
      try { const out = await run(JSON.parse(body)); res.end(JSON.stringify(out)); }
      catch (e) { res.statusCode = 500; res.end(JSON.stringify({ error: String(e) })); }
    });
  }).listen(port, '127.0.0.1', () => console.log('driver on', port));
})();
