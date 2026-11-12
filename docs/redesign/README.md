# Glamour Studio plugin redesign: Character Sheet in the game

A proposal and mock-up (November 2026) for carrying the site's new look, _Character Sheet_ (built on
the site on 11 November 2026: koko-puffs/kokos-tools, `docs/tools/glamour-studio/overview.md`), into
the plugin: its window, every screen and state, and the panels beside the game's windows. Nothing in
the plugin's code has changed; this is for review first.

## Opening it

Open [index.html](index.html) in a browser, straight from the folder: no build, no network (the fonts,
icons and pictures are in `assets/`). The deck has the idea, the colours, the type, the logo in the
window, the Kit's parts, what changes from today, every screen and the list of every feature with
where it went. Each page draws its screens at their real size (100% scale) on a blurred stand-in for
the game, with numbered notes under each. On any page, **B** outlines what's a texture or a special
draw-list shape, and **P** hides the pins.

`thumbs/` has each page's first frame at 2× (the deck's pictures).

## The screens

| Page | Frames |
| --- | --- |
| [Home](pages/home.html) | At the dresser with a plan (the field, Fill plate 3) · filled, someone examined · away from a dresser · the first run, not linked |
| [Looks](pages/looks.html) | Your looks, one picked · with Glamour Plates open (the plate slip) · Wear today · empty, loading, nothing found, a refused token |
| [A look](pages/look.html) | Ready with the editor open · not ready (put away first, Into the armoire) · Put on a plate… · a shot for the site |
| [Plates](pages/plates.html) | All twenty, one picked, gearsets · Link to a gearset, Wear it blocked · not read yet |
| [Closet](pages/closet.html) | Where your gear is, on the site, the wishlist · sending, a problem, off, not asked, not linked, empty |
| [Make room](pages/make-room.html) | The dresser's number · it asks first (one set) · storing, with Stop · done, one left · take out duplicates |
| [Settings and the recorder](pages/settings.html) | General · the whole column · recording, stopped, saved · Account linked, characters · not linked, a bad token · Forget this character? |
| [Asking about a character](pages/characters.html) | The question over the game, the chat lines · the pill's character states |
| [Beside the dresser and Glamour Plates](pages/companions-1.html) | The dresser's panel, Plate it's menu, folded, Make room pressed twice, working, not linked · Glamour Plates with a plan, filled, saved, unsaved, no plan · the Character window's plates |
| [Beside the other windows](pages/companions-2.html) | Armoire · storing an outfit set · fitting room · Examine · dye · a retainer · market board · a shop · tooltip lines · the right-click menu |
| [Popups, toasts, the small window](pages/extras.html) | 900 × 600 · toasts, tooltips, a combo, every state of the header's pill, a failed screen, refreshing |

## The main decisions

- **The rail becomes the site's bar.** Home · Looks · Plates · Closet · Make room across the top with
  their counts and a holo underline under the one you're on; your character's initials and how its
  closet is sent move into the pill at its right. The screens get the rail's 212px back. Dalamud's
  title bar (▼, ☰, ×) stays Dalamud's.
- **One field a screen, at most**, for the moment you came for: the plate open at a dresser (Home), a
  look's sheet (Looks, a look), the dresser's number (Make room), the first run. Closet, Plates and
  Settings have none.
- **One sticker a screen**: Fill plate 3, Put on plate 3, Try it on, Wear it, Make room for me, Yes,
  send it, Link. Turned over to ink on the field. Disabled, it stays a sticker at half strength with
  its reason as a tooltip.
- **A look is a character sheet**: its pieces as callouts round its middle on the field (the game's
  gear-window order), the name echoed behind, the palette strip, the maker's seal; the plate card
  becomes the site's plate slip (a ticket).
- **The honest colours replace mint and orchid**: owned ice, missing pink, the wishlist lavender, amber
  for what to see to (pieces in your bags, a dye to do), red only for what went wrong.
- **Settings stays plain**: a quiet column of cards and switches; the recorder at its foot.
- **The site's words** where the plugin shows the same thing: Wear today (was Wear now / You could
  wear now), Copy my game outfit (for your outfit; Keep it as a look stays for a plate or someone
  examined), Discover, Your page.

## Building it

What the C# Kit (`GlamourStudio/UI/Kit/`) would need. Not done here.

**Fonts** (`Resources/Fonts`, OFL, their licences beside them):

- Bricolage Grotesque **ExtraBold (800), static**: made from the variable font the site uses
  (`fonttools varLib.instancer <the wght variable font> wght=800`), as the site fixes it at 800. Sizes 13, 15, 17, 20, 26, 34, 44, 60; the echo at 72 and 104 with only
  A–Z, 0–9 and a few signs in its glyph range, to keep the atlas small. Replaces Outfit (Bold and
  SemiBold go).
- Shantell Sans at one weight (about 560: a static instance too), sizes 13, 15, 18.
- Figtree: the four weights kept, plus ExtraBold (800) for the sticker's words. Geist Mono kept.
- `Fonts.cs`: the new faces and the size table above in place of Outfit's.

**Textures** (embedded, loaded once):

- `logo.png` at 1× and 2× (from `public/favicon-glamour.svg` on the site), drawn at 18, 22, 30, 48
  and 96. The plugin's `images/icon.png` becomes the tag too (the site's `npm run marks` writes it).
- `holo.png`, a 256px tile of the holo marble: the tab underline (a strip of it, its UVs slid slowly),
  the sparkle, the New tag.
- `dots.png`, a 14px tile of the field's dots (UV-repeated over the field).
- `cloud.png` (tinted per use) and `spark.png` (the holo four-point star).
- The game's item icons as today (`ITextureProvider`); the shot's preview as today.

**New or changed Kit parts:**

- `Palette`: the new values (the deck's colour sheet), `Field`, `FieldHi`, `FieldDeep`, `FieldTone`,
  `OnField`, `OnField2`, `StickerShadow`, `Ice`, `Pink`, `Starred`, `Amber`; mint, orchid and the iris
  gradient go.
- `Field(rect)`: the filled rect, its floor (`AddRectFilledMultiColor`, transparent to FieldDeep at
  55% from 62% down) and the dots texture, with a push that turns words, eyebrows, outlines and the
  sticker to the field's colours inside it (as `.gs-moment` does on the site).
- `Echo(text)`: the name at 104 (or 72) in FieldTone, clipped to the field, a few rows offset.
- `Sticker(label, size)`: fill, 2px edge, the block as a second rect 3px down-right; hovered FieldHi;
  pressed drawn 3px over with no block; turned on the field.
- `Callout(rect)`: card, 2px black edge, a 4px black block.
- `Slip`: a ticket (rounded rect, two circles of the ground cut at its sides, a dashed line).
- `Tag(kind)`: ok, miss, star, amber, danger, ink, new (holo), on-field.
- `Seal` and `Sign`: drawn, then their vertices turned (record the vertex buffer's size, draw, rotate
  the new vertices about the centre): 4° and 5°.
- `Ring`: two arcs (`PathArcTo`, stroked). `Palette strip`: filled rects with the dyes' names.
- `Bar` in place of `DrawRail`: the logo and wordmark, the places (`NavItem` becomes a tab with the
  holo underline), the who-pill (`StatusPill` with the character's initials), Refresh, Settings;
  icons only under 1000 wide. `RailWidth` goes.
- `EmptyState`: the sign, the cloud, a line, an action.
- `Toast`: the sparkle at its start for good news, amber with a warning sign otherwise.
- `Theme`: popups and menus 16px corners on Raised ink with LineStrong; tooltips 10px; the question
  card without its glow loop; the title bar on Ground2.
- The panels' header: the logo, GLAMOUR STUDIO, the title in display capitals.

**Words** to change with it: Wear now and You could wear now → Wear today; your outfit's Keep it as
a look → Copy my game outfit. When it's built, DETAILS.md's “The window” and “Beside the game's
windows” say so, and the README's credits name Bricolage Grotesque and Shantell Sans for Outfit.

**Open questions**: whether the bar's five places fit with long counts at 1000–1100 wide in every
language the game runs in (the mock-up is English); whether the echo's 104px atlas is worth it or
72px is enough; whether the holo underline should slide (it does on the site).
