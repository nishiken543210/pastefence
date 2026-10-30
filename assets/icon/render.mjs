// SVG を PNG にする。描画は環境に入っている Chromium（Playwright）に任せる。
// 使い方: node assets/icon/render.mjs <出力フォルダー>
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

// playwright はこのリポジトリではなく環境側に入っている。
// ESM の import は NODE_PATH を見ないので require 経由で読む。
const { chromium } = createRequire(import.meta.url)('playwright');

const here = dirname(fileURLToPath(import.meta.url));
const outDir = resolve(process.argv[2] ?? resolve(here, 'out'));
mkdirSync(outDir, { recursive: true });

// 寸法ごとに、どちらの SVG を使うか。
// 32px 以下は大きい版だと伏せ字 ■■■ の隙間が 1px を切って潰れるので、
// 細部を削った版を使う。
const jobs = [
  { size: 16, svg: 'pastefence-small.svg' },
  { size: 24, svg: 'pastefence-small.svg' },
  { size: 32, svg: 'pastefence-small.svg' },
  { size: 48, svg: 'pastefence.svg' },
  { size: 64, svg: 'pastefence.svg' },
  { size: 128, svg: 'pastefence.svg' },
  { size: 256, svg: 'pastefence.svg' },
];

const browser = await chromium.launch();
try {
  for (const { size, svg } of jobs) {
    const markup = readFileSync(resolve(here, svg), 'utf8');
    const page = await browser.newPage({
      viewport: { width: size, height: size },
      deviceScaleFactor: 1,
    });
    // SVG を寸法どおりに置く。余白・背景色が混ざらないようにする。
    await page.setContent(
      `<!doctype html><meta charset="utf-8">
       <style>html,body{margin:0;padding:0;background:transparent}
       svg{display:block;width:${size}px;height:${size}px}</style>
       ${markup.replace(/\swidth="\d+"/, '').replace(/\sheight="\d+"/, '')}`,
      { waitUntil: 'load' },
    );
    const png = await page.screenshot({ omitBackground: true, type: 'png' });
    writeFileSync(resolve(outDir, `icon-${size}.png`), png);
    await page.close();
    console.log(`icon-${size}.png  (${svg})`);
  }
} finally {
  await browser.close();
}
