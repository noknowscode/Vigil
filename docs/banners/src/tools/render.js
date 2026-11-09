// node render.js <html> <out.png> <W> <H> <scale> [<html> <out> <W> <H> <scale> ...]
// Renders a composition at an exact CSS size. The scale is applied as CSS zoom on the root at device scale 1, not as
// deviceScaleFactor: headless Chromium rasterizes perspective-transformed layers below the device scale (soft 2x
// output), while a zoomed layout is laid out and rasterized at full size. Geometry is identical either way.
const { chromium } = require('playwright');
(async () => {
  const jobs = [];
  const a = process.argv.slice(2);
  for (let i = 0; i < a.length; i += 5) jobs.push(a.slice(i, i + 5));
  const b = await chromium.launch();
  for (const [html, out, W, H, s] of jobs) for (let attempt = 1; attempt <= 3; attempt++) try {
    const ctx = await b.newContext({ viewport: { width: Math.round(+W * +s), height: Math.round(+H * +s) }, deviceScaleFactor: 1 });
    const p = await ctx.newPage();
    await p.goto('file://' + require('path').resolve(html));
    if (+s !== 1) await p.evaluate(z => { document.documentElement.style.zoom = z; }, +s);
    await p.evaluate(() => document.fonts.ready);
    await p.waitForFunction(() => [...document.images].every(i => i.complete && i.naturalWidth > 0), null, { timeout: 30000 });
    await p.waitForTimeout(400);
    await p.screenshot({ path: out, clip: { x: 0, y: 0, width: Math.round(+W * +s), height: Math.round(+H * +s) } });
    await ctx.close();
    console.log('rendered', out);
    break;
  } catch (e) { console.log('retry', out, String(e).split('\n')[0]); if (attempt === 3) throw e; }
  await b.close();
})();
