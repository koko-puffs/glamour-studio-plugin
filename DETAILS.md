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
- **Plates planned on the site.** On the site's Closet → Plates, pick a look for each plate of a
  character; at a dresser the plugin's plate editor card offers Fill it with “that look”.
- **Wear now.** The public looks on the site you own every piece of, the most loved first.
- **Your shopping list ticks itself.** A piece on your list that the plugin sees in your wardrobe
  comes off it on the site (and a line in chat says so, if you like).
- **Make room in your dresser.** How full it is, the outfit sets whose loose pieces could be kept
  as one outfit, and the pieces none of your looks or plates uses.
- **Put away.** The pieces a look needs that are in your bags or armoury chest, and with the armoire
  open, Into the armoire for the ones it takes.
- **Gearsets.** A plate's linked gearsets, Link to a gearset, Unlink, Change to it.
- **Shots for the site.** Take a shot of a public look as worn (the HUD hides for a moment), see it,
  caption it and send it to the look's Worn in game.
- **Examine.** While the game's Examine window is open, the outfit you're looking at: Try it on, or
  Keep it as a look of yours.

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
  (empty, one of your looks by name, or so many pieces) and whether it's saved. With a look
  planned for that plate on the site: ✓ when it's on, else **Fill it with “the look”** (and › to
  see the look first). While it's open, the looks ready for a plate come first in the lists.
- **Examining** (only while the game's Examine window shows someone): their outfit as the window
  shows it (glamour and dyes; someone new is shown once two reads half a second apart agree, as the
  window can hold the last one's gear while the next loads), with Try it on and Keep it as a look (a private look of yours named
  "Their name's outfit", their body for the mannequin).
- **Get started** (only while not linked): three steps (link, open the dresser and armoire once,
  put a look on a plate) with Link your account and Open Glamour Studio.
- **Sent from the site:** looks you sent from the site (a look's ⋯ → Send to game as a plate, or
  the dressing room's ⋯ → Send to game), newest first, with the plate they were meant for. One
  goes off the list once it's on a plate.
- **The tabs:**
  - *Looks* and *Loved*: your looks and the public ones you love, with a search (name, maker, job,
    mood) and Refresh. Each row: four of its pieces' icons, its name, its pieces (and maker), and
    how ready it is for a plate (Ready for a plate, so many to store, so many of so many ready).
  - *Wear now*: the public looks on the site (`gs_plugin_wearable`, 60 at most) whose every piece
    is in your closet, the most loved first. Read when the window opens and every 10 minutes, and
    again after a sync that changed your closet.
  - *Plates*: what you're wearing, then each plate with something on it (the look it is, when it's
    one of yours). Shown once the game has your plates (you've opened them at a dresser).
  - *Closet*: the dresser, armoire and plates in numbers; each place with how many pieces and when
    it was read, or what to open so it can be; Make room in your dresser; your shopping list, each
    piece with where it is if you have it; and the site: what was sent and when, Send it now, Open
    my closet.

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

**Put away first** (when some pieces are yours but not in the dresser or armoire): each with where
it is and whether the armoire takes it. With the armoire's window open (at an inn), **Into the
armoire** stores one from your bags or armoury chest (`Cabinet.StoreCabinetItem` by its Cabinet
row; not yet watched in the game). Not offered for a piece you're wearing; once asked, the button
says Storing… for five seconds (the game answers in a moment, and a second ask would be a second store). The dresser has no such call the plugin knows: it says to store
them there.

**Worn in game** (public looks only: the site takes shots for those): **Take a shot** hides the HUD
(as Scroll Lock does) for a few frames, grabs the game's frame before Dalamud draws its windows,
shows the HUD again, and makes the picture 2560 at most on its long side (1920 if no quality fits)
as a JPEG under 3 MB, with a preview. Under the preview: a caption, **Send it to the look**, Take
another, Discard. Sent, it goes to the edge function `gs-plugin-shot` and shows on the look's page
under Worn in game as yours (three of yours a look, three a minute, twenty a day). A frame that
never comes is given up on after five seconds, and the HUD shown again; the plugin's every-frame
work runs each part on its own, so nothing else failing can keep the HUD hidden.

### A plate (or what you're wearing)

Its pieces, the look it is (when it's one of yours), and Keep it as a look (a new private look on
the site named "Plate 3" or "Koko's outfit", then Open it on the site), Try it on and Wear it
(`/glamourplate N`). For a plate: the look planned for it on the site (a link to it, ✓ when it's
on), and **Gearsets**: the ones it's linked to, each with Change to it (`EquipGearset`: the plate
comes with it) and × to unlink, and Link to a gearset (a menu of the others, with the plate each
has now; `LinkGlamourPlate`, saved by the game as its own gearset window does).

### Make room in your dresser

From Closet. The dresser's places used of 800 (an outfit's pieces share one), free, and up to how
many gathering outfits would free (a loose piece two sets share counted once). **Keep as outfits**: each outfit set (`MirageStoreSetItem`) with two
or more loose pieces in the dresser (one, when the set's already stored), the most freed first.
A stored set's own pieces don't count as joining it again. **Not in any look or plate**: loose
pieces none of your looks, the looks you love, looks sent or planned for this character, your
plates or your outfit uses (shown only once your looks have been read: unlinked, or before the
first read, it says so rather than calling everything unused). It only shows: the storing and taking out are the
dresser's own.

## Settings

**General**

| Setting | Default | What it does |
| --- | --- | --- |
| Empty the slots a look leaves bare | on | A look with no earrings empties the plate's earrings, so the plate is the look exactly. Weapons are left alone unless the look has one (a plate's weapon is its job's). |
| Dye pieces with the dyes in your bags | off | A piece whose copy is dyed otherwise than the look gets the look's dyes from bottles in your bags (the game spends them when the plate is saved). New, and not yet watched in the game: off, the plugin says which pieces to dye in the plate editor. |
| Open with the plate editor | on | Opening Glamour Plates opens the window. |
| Keep it up to date by itself | on | Sends your closet as it changes (see [The site](#the-site)). |
| Send my plates and outfit | on | Your plates and what you wear go along, with your character's name and world (only you see them). |
| Say in chat when a wish comes true | on | A line in chat when pieces on your shopping list are seen in your wardrobe (they come off the list either way). |
| Forget this character | | Forgets what the plugin has read of the character you're playing (it's read again as you play). |

**Account:** the token (pasted, shown masked once linked, Unlink), Get a token (the site's
Settings → Glamour Studio → In-game plugin), and the site. The version and What's new at the foot.

## Reading your wardrobe

`Game/WardrobeReader.cs`, on the framework thread every two seconds. Only gear is kept (an item
with an equip slot, not a soul crystal), by its normal-quality id (a high-quality copy's id less a
million).

| Place | Read from | When the game has it |
| --- | --- | --- |
| Bags | `Inventory1`–`4` | once every container is loaded |
| Armoury chest | `EquippedItems` and the `Armory*` containers | once every container is loaded |
| Saddlebags | `SaddleBag1`–`2`, `PremiumSaddleBag1`–`2` | once you've opened them since logging in (`InventoryBuddy`) |
| Glamour dresser | `MirageManager.PrismBoxItemIds` and its two stain arrays (800 places); an outfit set (`MirageStoreSetItem`) counts as its pieces stored in it (`IsSetSlotUnlocked`) | once you've opened it (or Glamour Plates) since logging in, and `PrismBoxLoaded` |
| Armoire | each `Cabinet` row's item, if `UIState.Cabinet.IsItemInCabinet` | once you've opened it since logging in, and `IsCabinetLoaded` |
| Retainers | `RetainerPage1`–`7` and `RetainerEquippedItems`, by the active retainer's id | a second after a retainer's bags open (so the pages are its own), every page loaded |
| Plates | `MirageManager.GlamourPlates` (20 plates of 12 slots: item and two stains each) | once you've opened Glamour Plates since logging in, and `GlamourPlatesLoaded` |
| What you wear | `EquippedItems`: each slot's glamour (`GlamourId`) or the piece, and its stains | always |

A place is only taken as read when the game has really sent it: its window seen open since this
character logged in (the containers can say they're loaded while empty, or still hold the last
character's), or every one of its containers loaded. A place not read keeps what was read before.
What's read is kept per character in `wardrobe.json` in the plugin's folder (written 30 s after a
change at the soonest, off the framework thread, numbered so an older write never lands last, and
on unloading), so a place read yesterday still counts today. A character is kept by a key made from its id (SHA-256 of it, 22 letters of
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
| `gs_plugin_sync` | your wardrobe changed and settled for 10 s, at most every 3 min (and only when what's sent changed); Send it now; linking | the character you're on (key, name, world), its pieces each where they are, the places read in full, its plates and what it wears |
| `gs_plugin_looks` | the window opens, every 10 min while it's open, Refresh | your looks (300), the public ones you love (200), your shopping list's items |
| `gs_plugin_inbox_list` | every minute while the window's open, every 5 otherwise | the looks sent to the game, each whole, with the plate meant |
| `gs_plugin_inbox_remove` | a sent look went on a plate | it's off the list |
| `gs_plugin_save_look` | Keep it as a look (a plate, your outfit, someone examined) | a new private look of yours |
| `gs_plugin_plans` | the window opens and with the looks, and when you change character (30 s at most) | that character's plate plans, each look whole |
| `gs_plugin_wearable` | the window opens, every 10 min while it's open, after a sync that changed the closet | public looks you own every piece of, the most loved first |
| `gs-plugin-shot` (edge function) | Send it to the look | multipart: token, look, caption, the JPEG; the site checks the token, the look (public), the limits, and stores the shot |

`gs_plugin_sync`'s answer has `wishes_got`: the items it took off your shopping list because this
sync had them.

On the site, each character's reads are kept apart (`gs_closet_reads`), and the plugin's pieces in
your closet are made again from all of them, each in its handiest place. So another PC, or one
character played alone, never takes another character's pieces off; pieces you ticked by hand stay
yours until the plugin sees them somewhere. Plates and what
you wear are kept per character (eight at most), and the site's Settings tab lists them, to forget.

## The code

| Folder | What |
| --- | --- |
| `Plugin.cs`, `Services.cs`, `Configuration.cs` | Loading, the command, the framework tick (reading, the site, opening with the plate editor), settings. |
| `Core/` | Game-free, tested: slots and the game's orders (`Slots`), looks and their JSON (`Looks`), the wardrobe and what's sent (`Wardrobe`), the plan for a plate (`PlatePlan`), how ready a look is (`Readiness`), the site's calls (`SiteLink`), words (`Words`). |
| `Game/` | The game: its sheets (`GameData`: items, dyes, armoire rows, outfit sets, jobs), reading your wardrobe (`WardrobeReader`), the plate editor, try-on and wearing a plate (`PlateEditor`), the armoire (`Armoire`), gearsets (`Gearsets`), the Examine window (`Examine`). |
| `Session/` | `SiteSync`: the site in the background. `Shots`: taking, making and sending a shot. |
| `UI/` | `MainWindow` and its screens (`Views/Home`, `LookView`, `PlateView`, `CleanupView`, `Settings`, `Shared`); `Kit/` the controls, colours, fonts and icons; `Model/` the kit's game-free parts (SVG paths). |
| `GlamourStudio.Tests/` | xUnit over `Core/` and `UI/Model/`: builds and runs without the game or Dalamud. |

## Releasing

Push a tag like `v0.1.0` (or run the Release workflow by hand with a version: it builds `main`,
whichever branch it's started from). It builds against
the current Dalamud, runs the tests, makes a GitHub release with `GlamourStudio.zip`, and points
`repo.json` at it (making the plugin's entry on the first release: `repo.json` starts empty, so
Koko's repository lists nothing for it until then). Koko's repository
(`https://www.kokos-tools.com/dalamud/repo.json`, the site's `api/dalamud-repo.js`) reads this
repo's `repo.json` with Housing Studio's and Raid Planner's, so players who added it for either see
this one too, within a few minutes of a release.

The workflows need Settings → Actions → General → Workflow permissions → Read and write.

## Release notes

### 0.2.0

- **Plates planned on the site**: pick a look for each plate on the site (Closet → Plates); at a
  dresser, the plate editor's card offers Fill it with that look, and a plate's screen shows its plan.
- **Wear now**: a new tab with the public looks you own every piece of.
- **Your shopping list ticks itself** when the plugin sees a piece in your wardrobe (and says so in chat).
- **Make room in your dresser**: how full it is, outfit sets to gather, pieces nothing uses.
- **Put away first** on a look, with Into the armoire while the armoire's open.
- **Gearsets** on a plate's screen: linked ones, Change to it, Link to a gearset, Unlink.
- **Take a shot** of a public look for its Worn in game on the site.
- **Examine**: someone's outfit while you examine them, to try on or keep as a look.

### 0.1.2

0.1.1 as it was meant to be: 0.1.1's download was built from 0.1.0's code (the Release workflow,
run by hand, built the repository's default branch, then still an old one). The workflow now always
builds `main` when run by hand. Everything under 0.1.1 below is in this one.

### 0.1.1

- Your closet on the site can't lose pieces any more by being read before the game has sent a
  place: saddlebags, the dresser, the armoire and plates count only once you've opened them since
  logging in, bags and the armoury chest once all their containers are there, a retainer a second
  after its bags open. (0.1.0's empty saddlebags read too soon are forgotten.)
- Each character's closet is sent on its own and kept apart on the site, so playing on two PCs, or
  one character at a time, takes nothing off the others. Include my other characters is gone (it's
  always so now).
- After filling a plate, its card (and Wear plate N) stays once the plate editor's closed.
- Steadier: answers for a token you've just changed are let go; a sent look you've put on a plate
  doesn't come back for a moment; Keep it as a look remembers which character's plate it was.
- Lighter: the plan for a plate, the plate editor's state and the lists are worked out once per
  change, not every frame; wardrobe.json is written at most every 30 s; nothing is worked out again
  to send when nothing changed; looks sent from the site are looked for every 15 minutes while the
  window's closed (at once when it opens).
- The window: no line over the title, and 490 wide at the least so the title fits.
- A link of several words (Settings' What's new) lights up as one when pointed at, not word by word.
- The rose pill and switches in the site's deeper rose with white words, as its buttons and the
  mark now are; rose words, icons and rings on the dark stay the bright rose.

### 0.1.0

The first version.

- Put a look on the glamour plate open in the plate editor: each piece from the dresser or the
  armoire, bare slots emptied (weapons kept), what's missing and what needs dyeing said first.
- Try a look or a plate on in the fitting room; wear a plate.
- Reads your bags, armoury chest, saddlebags, glamour dresser (outfit sets as their pieces),
  armoire, retainers, plates and what you wear, and remembers them per character.
- Linked to Glamour Studio: your closet, plates and outfit sent as they change; your looks and
  the ones you love; looks sent from the site; Keep it as a look.
