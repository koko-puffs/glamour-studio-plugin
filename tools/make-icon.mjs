// Renders tools/icon.svg (the site's mark: koko-puffs/kokos-tools src/features/marks.ts, the
// clothes tag with its hanger, with the soft light the plugins' icons have) with Playwright's
// Chromium (require('playwright') from anywhere it's installed, or PLAYWRIGHT_PATH) to:
// - GlamourStudio/images/icon.png, 512 × 512 (what Dalamud's installer shows);
// - GlamourStudio/Resources/Textures/logo-32/64/128/256.png, the logo the window draws (Kit.Logo):
//   the same tag without the light, as the site's favicon-glamour.svg.
// When the site's mark changes, write icon.svg again from it (markSvg('glamour-studio', 512, true))
// and run this: node tools/make-icon.mjs
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const plugin = join(here, '..', 'GlamourStudio')
const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_PATH ?? 'playwright')

const icon = readFileSync(join(here, 'icon.svg'), 'utf8')
// The logo: the tile without the light laid over it.
const logo = icon.replace(/\n\s*<path [^>]*fill="url\(#shine\)"\/>/, '')
if (logo === icon) throw new Error("icon.svg has no light to take off for the logo (its path filled url(#shine))")

/** Draws an SVG `size` px across (its corners clear) to `file`. */
async function render(browser, svg, size, file) {
  const page = await browser.newPage({ viewport: { width: size, height: size } })
  const sized = svg.replace(/<svg ([^>]*?)width="\d+" height="\d+"/, `<svg $1width="${size}" height="${size}"`)
  await page.setContent(`<html><body style="margin:0;background:transparent">${sized}</body></html>`)
  await page.screenshot({ path: file, omitBackground: true })
  await page.close()
  console.log(`Wrote ${file.slice(join(here, '..').length + 1)}`)
}

const browser = await chromium.launch()
try {
  await render(browser, icon, 512, join(plugin, 'images', 'icon.png'))
  for (const size of [32, 64, 128, 256]) await render(browser, logo, size, join(plugin, 'Resources', 'Textures', `logo-${size}.png`))
} finally {
  await browser.close()
}
