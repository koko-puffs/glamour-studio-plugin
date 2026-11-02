# Glamour Studio plugin: the details

The full reference: every screen and setting, how a plate is filled, what the plugin reads and
sends, how the code is laid out, releasing, and the release notes. The short version is the
[README](README.md).

- [What it does](#what-it-does)
- [The window](#the-window)
- [Settings](#settings)
- [Reading your wardrobe](#reading-your-wardrobe)
- [Filling a plate](#filling-a-plate)
- [The site](#the-site)
- [The code](#the-code)
- [Releasing](#releasing)
- [Release notes](#release-notes)

## What it does

Glamour Studio (the site, `kokos-tools.com/glamour-studio`) is where looks are made: a mannequin
dressed in any piece of gear, dyed, saved, shared. The plugin is its half in the game:

- **Looks on plates.** With the game's plate editor open (a glamour dresser → Glamour Plates),
  a look from the site goes on the plate open there in one press: each piece is found in the
  glamour dresser (or the armoire), the plate's slots set, the ones the look leaves bare emptied.
  You check it and press Save in the game: nothing is saved for you.
- **Try it on.** Any look, or any plate, in the game's fitting room, wherever you are.
- **Your closet on the site.** Linked with a token, it sends what it has read of your wardrobe:
  each piece of gear you own and its handiest place (dresser, armoire, armoury chest, bags,
  saddlebags, a retainer), your glamour plates and what you're wearing. The site's closet, "6/8 in
  your closet", Only looks I can wear today and the shopping list all read from it.
- **Both ways.** Looks you Send to game on the site wait in the plugin (Sent from the site); a
  plate or your outfit can be kept as a new private look on the site (Keep it as a look), and the
  dressing room's Copy my outfit from the game reads what it sent.

`/gstudio` opens the window, `/gstudio settings` opens Settings, `/gstudio send` sends your
closet now.

## The window

One window in the site's look (Glamour Studio's dark night sky and its rose: `UI/Kit/Palette.cs`
holds the site's `--gs-*` colours; Syne 800 capitals for titles, DM Sans to read, DM Mono in
spaced capitals for labels and eyebrows: `UI/Kit/Fonts.cs`). Its layout and controls are Housing
Studio's and Raid Planner's plugins' (`UI/Kit`, the same kit in this tool's colours): the back
arrow at the top left of every screen below Home, Settings behind the sliders button at the top
right, a card of rows for settings, the question as a card over the window.

### Home

- **The header:** the title, Settings, and a pill for what's going on: the plate editor open (and
  on which plate), not linked, sending your closet, sent and when, or what went wrong.
- **The plate editor's card** (only while it's open): the plate open in the game, what's on it
  (empty, one of your looks by name, or so many pieces) and whether it's saved. While it's open,
  the looks ready for a plate come first in the lists.
- **Get started** (only while not linked): three steps (link, open the dresser and armoire once,
  put a look on a plate) with Link your account and Open Glamour Studio.
- **Sent from the site:** looks you sent from the site (a look's ⋯ → Send to game as a plate, or
  the dressing room's ⋯ → Send to game), newest first, with the plate they were meant for. One
  goes off the list once it's on a plate.
- **The tabs:**
  - *Looks* and *Loved*: your looks and the public ones you love, with a search (name, maker, job,
    mood) and Refresh. Each row: four of its pieces' icons, its name, its pieces (and maker), and
    how ready it is for a plate (Ready for a plate, so many to store, so many of so many ready).
  - *Plates*: what you're wearing, then each plate with something on it (the look it is, when it's
    one of yours). Shown once the game has your plates (you've opened them at a dresser).
  - *Closet*: the dresser, armoire and plates in numbers; each place with how many pieces and when
    it was read, or what to open so it can be; and the site: what was sent and when, Send it now,
    Open my closet.

### A look

Its name under an eyebrow (Your look, Loved · by its maker, Sent from the site), then the one
thing to do:

- **With the plate editor open:** Put on plate N (the plate open in the game) and, under it, what
  it will do: how many pieces go on, which are elsewhere (put them in your dresser first) or not
  yours, which will need dyeing, which dyes come from your bags, that bare slots are emptied, and
  that nothing's saved until you press Save. After filling: On plate N, with Wear plate N (once
  you've saved it) and Fill it again.
- **Without it:** how to get there (a glamour dresser → Glamour Plates).
- Try it on (the fitting room) and On the site (the look's page).

Then its pieces: each its icon, name, slot and dyes, and a chip for where it is: Dresser or
Armoire (green: a plate can take it), Armoury chest, Bags, Saddlebags or Retainers (put it in your
dresser first), On your list (your shopping list on the site) or Missing. The chip's tooltip says
what to do. With the editor open, a piece whose dyes won't be the look's says so.

### A plate (or what you're wearing)

Its pieces, the look it is (when it's one of yours), and Keep it as a look (a new private look on
the site named "Plate 3" or "Koko's outfit", then Open it on the site), Try it on and Wear it
(`/glamourplate N`).

## Settings

**General**

| Setting | Default | What it does |
| --- | --- | --- |
| Empty the slots a look leaves bare | on | A look with no earrings empties the plate's earrings, so the plate is the look exactly. Weapons are left alone unless the look has one (a plate's weapon is its job's). |
| Dye pieces with the dyes in your bags | off | A piece whose copy is dyed otherwise than the look gets the look's dyes from bottles in your bags (the game spends them when the plate is saved). New, and not yet watched in the game: off, the plugin says which pieces to dye in the plate editor. |
| Open with the plate editor | on | Opening Glamour Plates opens the window. |
| Keep it up to date by itself | on | Sends your closet as it changes (see [The site](#the-site)). |
| Include my other characters | on | The closet sent is every character's this PC has read, each piece in its handiest place; off, only the one you're playing. |
| Send my plates and outfit | on | Your plates and what you wear go along, with your character's name and world (only you see them). |
| Forget this character | | Forgets what the plugin has read of the character you're playing (it's read again as you play). |

**Account:** the token (pasted, shown masked once linked, Unlink), Get a token (the site's
Settings → Glamour Studio → In-game plugin), and the site. The version and What's new at the foot.

## Reading your wardrobe

`Game/WardrobeReader.cs`, on the framework thread every two seconds. Only gear is kept (an item
with an equip slot, not a soul crystal), by its normal-quality id (a high-quality copy's id less a
million).

| Place | Read from | When the game has it |
| --- | --- | --- |
| Bags | `Inventory1`–`4` | always |
| Armoury chest | `EquippedItems` and the `Armory*` containers | always |
| Saddlebags | `SaddleBag1`–`2`, `PremiumSaddleBag1`–`2` | once you've opened them this session |
| Glamour dresser | `MirageManager.PrismBoxItemIds` and its two stain arrays (800 places); an outfit set (`MirageStoreSetItem`) counts as its pieces stored in it (`IsSetSlotUnlocked`) | once you've opened it this session (`PrismBoxLoaded`) |
| Armoire | each `Cabinet` row's item, if `UIState.Cabinet.IsItemInCabinet` | once you've opened it this session |
| Retainers | `RetainerPage1`–`7` and `RetainerEquippedItems`, by the active retainer's id | while a retainer's inventory window is open |
| Plates | `MirageManager.GlamourPlates` (20 plates of 12 slots: item and two stains each) | once you've opened them (`GlamourPlatesLoaded`) |
| What you wear | `EquippedItems`: each slot's glamour (`GlamourId`) or the piece, and its stains | always |

What's read is kept per character in `wardrobe.json` in the plugin's folder (written off the
framework thread, numbered so an older write never lands last), so a place read yesterday still
counts today. A character is kept by a key made from its id (SHA-256 of it, 22 letters of
base64url: `Wardrobe.KeyOf`), never the id itself; its name and world go along for the site.

The game's slot orders (`Core/Slots.cs`): a plate's slots go main hand, off hand, head, body,
hands, legs, feet, ears, neck, wrists, right ring, left ring; what you wear has the old waist at 5
and the soul crystal at 13; an outfit set lists main hand … bracelets and one ring.

## Filling a plate

`Core/PlatePlan.cs` works out what to do before anything is touched (tested on its own), and
`Game/PlateEditor.cs` does it.

**The plan.** For each of the plate's twelve slots:

- The look has a piece: the dresser's copy whose dyes match the look's most (a copy in an outfit
  set after a loose one, then the first in the dresser), each copy used once (two of the same ring
  need two copies); else the armoire's (it keeps pieces undyed); else it's missing, and where else
  it is (the plugin's record) is said.
- The look leaves it bare: emptied (Empty the slots a look leaves bare), except a weapon, which is
  kept.
- Dyes: a channel the item doesn't take doesn't count. A channel whose copy has another dye is
  dyed from a bottle in your bags when that's on and there's one left (each bottle counted once),
  else it's left for you to dye in the plate editor.

**Doing it.** With the plate editor open (`MiragePrismMiragePlate`), through the game's own agent
(`AgentMiragePrismMiragePlate`), as choosing in its window does: for each slot, the slot is picked
(`Data->SelectedItemIndex`) and given its copy with `SetSelectedItemData` (the dresser
(`PrismBox`, its place) or the armoire (`Cabinet`, its row), the copy's item id and dyes), or
emptied with `ClearItemData`. Dyes from your bags go on with `SetSelectedItemStains` (the bottles'
inventory items, as dragging a dye onto the slot does). The slot picked before is put back and the
plate marked changed (`HasChanges`), so the game asks before you leave it unsaved. The dresser and
armoire are read again the moment before. Nothing is saved: the game's Save does that.

Not yet watched in the game (written from FFXIVClientStructs' signatures and what Glamaholic
found): a piece of an outfit set given as its set's place and its own item id, and the dyes from
your bags (off by default). Tell us how they go.

**Try it on** is `AgentTryon.TryOn` for each piece with its dyes; **Wear it** sends
`/glamourplate N` (`UIModule.ProcessChatBoxEntry`), which the game allows in a sanctuary, for a
job whose gear the plate fits.

## The site

Linked exactly as Housing Studio's and Raid Planner's plugins are: a token made on the site
(Settings → Glamour Studio → In-game plugin), shown there once (only its SHA-256 is kept),
`gsp_` and 43 letters, revoked there. The plugin calls the site's database functions
(`supabase/migrations/…_gs_plugin.sql` in koko-puffs/kokos-tools) with the site's publishable key,
the token in the body only (never logged). 120 changes or heavy reads an hour a token.
`Core/SiteLink.cs` makes the calls (none throws: a lost connection or no answer is said in words;
the site's own refusals are shown as they are, its internal ones aren't), `Session/SiteSync.cs`
runs them off the framework thread and takes the answers back to it.

| Call | When | What |
| --- | --- | --- |
| `gs_plugin_sync` | your wardrobe changed and settled for 10 s, at most every 3 min (and only when what's sent changed); Send it now; linking | the closet (each piece once, its handiest place, 5000 at most), the places read in full, the character (key, name, world), its plates and what it wears |
| `gs_plugin_looks` | the window opens, every 10 min while it's open, Refresh | your looks (300), the public ones you love (200), your shopping list's items |
| `gs_plugin_inbox_list` | every minute while the window's open, every 5 otherwise | the looks sent to the game, each whole, with the plate meant |
| `gs_plugin_inbox_remove` | a sent look went on a plate | it's off the list |
| `gs_plugin_save_look` | Keep it as a look | a new private look of yours |

On the site, a place the plugin read in full loses the pieces it put there that aren't there any
more; pieces you ticked by hand stay yours until the plugin sees them somewhere. Plates and what
you wear are kept per character (eight at most), and the site's Settings tab lists them, to forget.

## The code

| Folder | What |
| --- | --- |
| `Plugin.cs`, `Services.cs`, `Configuration.cs` | Loading, the command, the framework tick (reading, the site, opening with the plate editor), settings. |
| `Core/` | Game-free, tested: slots and the game's orders (`Slots`), looks and their JSON (`Looks`), the wardrobe and what's sent (`Wardrobe`), the plan for a plate (`PlatePlan`), how ready a look is (`Readiness`), the site's calls (`SiteLink`), words (`Words`). |
| `Game/` | The game: its sheets (`GameData`: items, dyes, armoire rows, outfit sets), reading your wardrobe (`WardrobeReader`), the plate editor, try-on and wearing a plate (`PlateEditor`). |
| `Session/` | `SiteSync`: the site in the background. |
| `UI/` | `MainWindow` and its screens (`Views/Home`, `LookView`, `PlateView`, `Settings`, `Shared`); `Kit/` the controls, colours, fonts and icons; `Model/` the kit's game-free parts (SVG paths). |
| `GlamourStudio.Tests/` | xUnit over `Core/` and `UI/Model/`: builds and runs without the game or Dalamud. |

## Releasing

Push a tag like `v0.1.0` (or run the Release workflow by hand with a version). It builds against
the current Dalamud, runs the tests, makes a GitHub release with `GlamourStudio.zip`, and points
`repo.json` at it (making the plugin's entry on the first release: `repo.json` starts empty, so
Koko's repository lists nothing for it until then). Koko's repository
(`https://www.kokos-tools.com/dalamud/repo.json`, the site's `api/dalamud-repo.js`) reads this
repo's `repo.json` with Housing Studio's and Raid Planner's, so players who added it for either see
this one too, within a few minutes of a release.

The workflows need Settings → Actions → General → Workflow permissions → Read and write.

## Release notes

### 0.1.0

The first version.

- Put a look on the glamour plate open in the plate editor: each piece from the dresser or the
  armoire, bare slots emptied (weapons kept), what's missing and what needs dyeing said first.
- Try a look or a plate on in the fitting room; wear a plate.
- Reads your bags, armoury chest, saddlebags, glamour dresser (outfit sets as their pieces),
  armoire, retainers, plates and what you wear, and remembers them per character.
- Linked to Glamour Studio: your closet, plates and outfit sent as they change; your looks and
  the ones you love; looks sent from the site; Keep it as a look.
