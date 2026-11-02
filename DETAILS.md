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

One wide window in the site's look (Glamour Studio's dark night sky and its rose: `UI/Kit/Palette.cs`
holds the site's `--gs-*` colours; Syne 800 capitals for titles, DM Sans to read, DM Mono in
spaced capitals for labels and eyebrows: `UI/Kit/Fonts.cs`), 1120 × 760 to start with, 900 × 600 at
the least. As in Housing Studio's and Raid Planner's plugins, the back arrow is at the top left of a
screen below (a look, Settings) and Settings behind the sliders button at the top right; the
question is a card over the window.

- **The header** across the top: GLAMOUR STUDIO, a pill for what's going on (recording, the plate
  editor open and on which plate, not linked, sending your closet, sent and when, or what went
  wrong) and Settings. On a look: back, its eyebrow and name, On the site and Settings.
- **The rail** down the left: Home, Looks (how many of yours), Plates (how many have something on
  them, of 20), Closet (how many pieces you have), and under Tidy up, Make room (the dresser's
  places used, of 800). At its foot your character: name, world and how your closet's sent.
  Under 1000 wide the rail is its icons alone (names and counts as tooltips).
- Most screens are a list or grid, with the thing picked in a pane on the right.

### Home

- **At the glamour dresser** (the plate editor open): a rose card with the plate open (PLATE 3),
  what's on it (empty, one of your looks, so many pieces), whether it's saved, and with a look
  planned for that plate on the site, **Fill plate 3** (and See the look). Under it the plate's
  twelve slots now and, with a plan, after: what goes on, what's emptied, a dashed slot for a piece
  that's elsewhere, and how many dyes come from your bags. After filling: Wear plate 3 (once it's
  saved) and Fill it again. Closed, a quieter card says how to get there and how many plates you've
  planned.
- **Get started** (not linked): three steps side by side (link, open the dresser and armoire once,
  put a look on a plate), Link your account and Open Glamour Studio.
- **Ready for plate 3** (or for a plate): up to six cards, the planned look first, then yours and
  the ones you love whose every piece is in the dresser or armoire. All your looks goes to Looks.
- **On the right:** **Sent from the site** (looks you sent from the site, newest first, with the
  plate they were meant for; one goes off the list once it's on a plate) and, while the game's
  Examine window shows someone, **Examining**: their outfit as shown (glamour and dyes; someone new
  is shown once two reads half a second apart agree), Try it on and Keep it as a look (a private
  look named "Their name's outfit", their body for the mannequin). With neither, a few looks **you
  could wear now**.

### Looks

Tabs with counts: **Your looks**, **Loved**, **Wear now** (the public looks on the site you own
every piece of: `gs_plugin_wearable`, 60 at most, the most loved first, read when the window opens,
every 10 minutes and after a sync that changed your closet). A search (name, maker, job, mood),
then **Ready for a plate first** (on by default), a job and a mood. Each row: four of its pieces'
icons, its name, whose it is, and how ready it is (Ready for a plate, so many to store, so many of
so many yours). The look picked shows in the pane beside: its pieces' icons, eyebrow and name, its
mood and job, the plate card (below), Try on, Shot (public looks), Site, its pieces each with where
it is (green Dresser or Armoire; rose where it's to put away; On your list; Missing) and **Open the
look**.

### A look

Its pieces laid out as the game's gear window: weapon, head, body, hands, legs, feet down the left;
off hand, ears, neck, wrists and rings down the right, each with its icon, name, slot and dyes and
where it is (with the editor open, whether its dye comes from your bags or must be done in the
editor). An empty slot says whether the plate's is emptied or left. In the middle: how many pieces
are ready for a plate in a ring, what stands between it and a plate, its dyes and Try it on.

On the right:

- **The plate card.** With the plate editor open: Put on plate N and what it will do (how many go
  on, which are elsewhere or not yours, which need dyeing, which dyes come from your bags, that bare
  slots are emptied); nothing's saved until you press Save. A look sent for another plate says so.
  After filling: On plate N, with Wear plate N (once saved) and Fill it again. Closed: how to get
  there.
- **Put away first** (pieces that are yours but not in the dresser or armoire): each with where it
  is and whether the armoire takes it. With the armoire's window open (at an inn), **Into the
  armoire** stores one from your bags or armoury chest (`Cabinet.StoreCabinetItem` by its Cabinet
  row; not yet watched in the game). Not offered for a piece you're wearing; once asked, it says
  Storing… for five seconds (a second ask would be a second store). The dresser has no such call
  the plugin knows: it says to store them there.
- **Worn in game** (public looks only): **Take a shot** hides the HUD (as Scroll Lock does) for a
  few frames, grabs the game's frame before Dalamud draws its windows, shows the HUD again, and
  makes the picture 2560 at most on its long side (1920 if no quality fits) as a JPEG under 3 MB,
  with a preview. Under it a caption, **Send it to the look**, Take another, Discard. Sent, it goes
  to the edge function `gs-plugin-shot` and shows on the look's page under Worn in game (three of
  yours a look, three a minute, twenty a day). A frame that never comes is given up on after five
  seconds and the HUD shown again.

### Plates

All twenty plates at once, five across: each its number, its pieces' icons, the look it is (or how
many pieces), a rose dot when one's planned for it on the site, and the jobs of the gearsets linked
to it; an empty one dashed. Under them when the game last had them, and **What you're wearing**
(Try it on, Keep it as a look). The plate picked, in the pane beside: what's on it, its plan (a
link to the look, On it or Not on it yet), **Wear it** (`/gearset change <your gearset> N`), Try it on, Keep it as a
look (a new private look named "Plate 3", then Open it on the site), **Gearsets** (each linked one
with its job, number and name, Change to it (`EquipGearset`: the plate comes with it) and × to
unlink; Link to a gearset, a menu of the others with the plate each has now: `LinkGlamourPlate`)
and its twelve slots.

### Closet

The glamour dresser first (read when, places used of 800 with a bar, how many places gathering
outfit sets would free, Make room), then the armoire, armoury chest, bags, saddlebags and retainers
(how many pieces and when read, or dashed with what to open so it can be read), and **On the site**
(what was sent and when, Send now, Open). Beside it **your shopping list**: the pieces that just
came off it (seen in your wardrobe), and each piece with a look that uses it and where it is if you
have it now.

### Make room

The dresser's places used of 800 (an outfit's pieces share one), free, and up to how many places
gathering outfits would free (a loose piece two sets share counted once). **Keep as outfits**: each
outfit set (`MirageStoreSetItem`) with two or more loose pieces in the dresser (one, when the set's
already stored), the most freed first; a stored set's own pieces don't count as joining it again.
**Not in any look or plate**: loose pieces none of your looks, the looks you love, looks sent or
planned for this character, your plates or your outfit uses (shown only once your looks have been
read). It only shows: the storing and taking out are the dresser's own.

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

One column in the middle (640 at most), as the other plugins' Settings: General and Account as a
pick-one pill, each group a label over a card of rows.

**Account:** linked or not in a line, the token (masked once linked, with Unlink; else a field and
Link), Get a token (the site's Settings → Glamour Studio → In-game plugin) and Open Glamour Studio.
The version and What's new at the foot.

## Making a report: the recorder

Settings → General → **Making a report**, or `/gstudio record`. Watch-only, as Housing Studio's
recorder (`Game/Recorder.cs`): while on it writes down, with the time since it started,

- `OPEN` / `REFRESH` / `CLOSE` of game windows (the glamour ones: the plate editor, the dresser,
  outfit sets, the armoire, Examine, gearsets, try-on, dyes, menus, Yes/No, saddlebags, retainers'
  bags, with all their words; others with a few);
- `CALLBACK` every click a window sends, with its values (what doing it by hand sends), and a
  menu's words;
- `AGENT` every event the glamour agents receive (MiragePrismMiragePlate, PrismBox,
  PrismSetConvert, PrismItemDetail, MiragePrism, Cabinet, CabinetWithdraw, Inspect, GearSet, Tryon,
  Colorant, InventoryContext, Context, RetainerList), with its kind, values and what it returned,
  and for an item's menu which item it was opened on and from where. Their `ReceiveEvent` and
  `ReceiveEventWithResult` are hooked only when no more than two agents share the function (the
  header says what was hooked, where in the game, and what wasn't);
- `CHAT` system and error messages, `LOGMSG` the game's log messages by id, and toasts;
- `STATE` every change (looked at five times a second) to: where you are (territory, combat, duty,
  cutscene, loading, HUD, GPose), the plate editor (plate, slot picked, changes, how it was opened,
  each slot's item, where it came from and its dyes), the dresser and plates as the game has them,
  the armoire (open, loaded, how many stored), gearsets and their plates, Examine (the agent's
  request and current target, fetch state, the pieces shown; never the names of others), what you
  wear, your bags, armoury, saddlebags and retainer pages (with quantities and dyes), the dye
  bottles in your bags, the dresser's window (tab, page, the piece right-clicked, search and filters,
  what it's doing), the armoire's window (category, the item picked), storing an outfit set (the
  set, where its pieces are, into an existing outfit or not), the fitting room (loaded, keeping what's tried on or not, its list and what its view draws),
  retainers (ready, the one picked), which glamour windows are open, and the plugin (linked,
  sending, the wardrobe, a shot);
- `READ` each place the plugin reads anew, `SITE` each call to the site (its function, status, time,
  size; never the token), `LOGIN` / `LOGOUT`;
- `PLUGIN` what the plugin did (filling a plate slot by slot with the game's answer, dyes from
  bags, storing in the armoire, linking or changing gearsets, wearing a plate, trying on, a shot and
  hiding and showing the HUD for it);
- `MARK` a step you're about to do (the field in Settings, or `/gstudio mark 2. Fill plate 3`),
  each with the whole state.

**Write down the windows' values** (`/gstudio dump`) adds every value the open glamour windows
hold and the plate editor, dresser, plates and armoire in full, a hundred values to a line. The
same line again is counted ("the line above 12 times in all"), not written again. **Copy the
recording** puts it on the clipboard with what the plugin knows (the plugin's, Dalamud's and the
game's versions, the plugins loaded, the recorder's hooks, character, settings, what's been read,
the last send); **Save it as a file** (`/gstudio save`) writes it to
`pluginConfigs/GlamourStudio/recordings/gstudio-<time>.txt`. It keeps the last 20,000 lines and stops
by itself after two hours. It sends nothing anywhere, and leaves out other people's names (the
Examine window's words and values, the names in a menu on someone). `/gstudio mark` and
`/gstudio dump` start a recording when none is going, but not over a stopped one that hasn't been
saved or copied yet.

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
inventory items, as dragging a dye onto the slot does). The slot picked before is put back. The
dresser and armoire are read again the moment before. Nothing is saved: the game's Save does that.

"Not saved yet" is worked out by the plugin: the plate's slots against the plate as the game keeps
it saved (`MirageManager.GlamourPlates`), and any dye waiting (`StainState`). The agent's own
`HasChanges` isn't used or written: in a recording it stayed false through a removed piece and a
dye while the game asked to discard them.

The plate editor counts as open only at a glamour dresser (`Data->OpenMode` 0). The same window
opened from the Character window's Glamour Plate button (`OpenMode` 1) only shows and applies
plates, so the plugin neither fills it nor opens with it.

Dyes from your bags: the game now dyes with general **Spectrum** bottles (Standard Spectrum Dye,
Wide Spectrum #2…), each making many colours, as well as a colour's own bottle (seen in a
recording). The plugin counts, for each colour, the bottles that can make it (`GameData.StainsOfItem`,
from the Stain sheet's items), and uses a colour's own bottle first, a Spectrum one after.

Watched in the game (the second recording): the plugin's fill leaves each slot exactly as picking
the piece by hand does (the piece, its source and place, the copy's dyes, `StainState` 1 with no
dye waiting), the window shows it, Save opens the game's confirmation, and closing asks to discard.
A dresser piece is `PrismBox` and its place; an armoire piece `Cabinet` and its row; an outfit
set's piece the set's dresser place with the piece's own item id. Not yet watched: the dyes from
your bags (off by default).

**Try it on** (`Game/FittingRoom.cs`) is `AgentTryon.TryOn` for each piece with its dyes, one at a
time: the game draws only the last of the pieces sent in one frame (the second recording saw it say
"You try on…" for all eleven and show only the weapon). So it opens the fitting room with the first
piece (closing it first if it's open, so pieces tried on before don't stay), waits until the window
shows and its character has loaded, sends the pieces a frame apart, then checks what the
window's view draws and sends any piece missing once more. For each piece it sets the fitting
room's keep-what's-tried-on flag (`AgentTryon.SaveDeleteOutfit`, as Glamaholic does): without it
each piece replaces the one before, and the look cycles through the slots instead of building up.
Your own setting of it is put back when the try-on ends. The toast says how many pieces it shows;
a piece your race or gender can't wear is the game's to refuse. **Wear it** sends
`/gearset change <the gearset you're on> N` (`UIModule.ProcessChatBoxEntry`): the game has no
command for a plate alone (`/glamourplate` isn't one), and allows this in a sanctuary, for a job
whose gear the plate fits. Watched: it changes to the gearset and projects the plate in an inn, in a
city and out in a field ("Unable to fully apply…" is the game's for pieces your job or character
can't wear). The game also sets the gearset's instant portrait, as changing gearset always does.

### What recordings showed (2 November 2026)

The plate window, at a glamour dresser: opened from the dresser's callback `[5]`; a plate picked
with `[17, plate−1]`; a slot's menu with `[16, slot]` ("Remove Item Image from Plate", "Dye", "Remove
Dye" / "Restore Item"); Save `[14, 0]`, which with dyes opens `MiragePrismMiragePlateConfirm` (the
bottles to be used), answered `[0]`, then "Glamour plate N saved." (log message 4382); closing
`[18, 0]`, which with changes asks "Discard all changes?". From the Character window: `[15, plate−1]`
applies a plate ("Glamours projected from plate N.", 4364; 4378 when it can't fully). A dye picked in
the dye window waits on the slot as `StainState` 1 with its colour and bottle until saved.

By hand, a slot is picked with `[13, slot]`, then a dresser piece with the dresser's `[2, index on
the page]`; the dresser's `[5]` (with a slot picked) opens the armoire's list (`CabinetWithdraw`), where
`[1, index]` picks a piece. A slot's menu `[16, slot]` → Remove Item Image from Plate empties it.

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
| `Game/` | The game: its sheets (`GameData`: items, dyes, armoire rows, outfit sets, jobs), reading your wardrobe (`WardrobeReader`), the plate editor, try-on and wearing a plate (`PlateEditor`), the armoire (`Armoire`), gearsets (`Gearsets`), the Examine window (`Examine`), the recorder (`Recorder`, `AtkText`). |
| `Session/` | `SiteSync`: the site in the background. `Shots`: taking, making and sending a shot. |
| `UI/` | `MainWindow` (the header, the rail, the screens) and its screens (`Views/Home`, `LooksView`, `LookView`, `PlateView`, `ClosetView`, `CleanupView`, `Settings`, `Shared`); `Kit/` the controls, colours, fonts and icons (`Layout`: panes, the rail's items, tabs with counts, chips, the ring); `Model/` the kit's game-free parts (SVG paths). |
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

### 0.3.3

- **Try it on** wears the look's pieces together: each piece kept the one before it out (the
  fitting room cycled through the slots). The plugin now asks the fitting room to keep what's tried
  on while it sends the look, and puts your setting back after.

### 0.3.2

- **Try it on** shows the whole look: the pieces go to the fitting room one at a time once it's
  open, not all at once (which left only the weapon, or an accessory). If the fitting room is open
  already it starts afresh. The recording logs each piece sent and what the window shows.

### 0.3.1

From the first recording:

- **Wear it** works: it sends `/gearset change <your gearset> N` (there's no `/glamourplate`).
- The plugin fills plates only in the plate window opened at a glamour dresser, not in the one from
  the Character window (which only applies plates).
- "Not saved yet" is now right (it was always "saved"), and the plugin no longer writes the flag it
  went by.
- Dyes from your bags count the new Spectrum bottles for every colour they make.

### 0.3.0

- **A new, wider layout**: a rail down the left (Home, Looks, Plates, Closet, Make room, your
  character), Home with the plate open at the dresser and its slots now and after, Looks with the
  look picked beside the list, a look laid out as the game's gear window, all twenty plates at
  once, Closet with your shopping list beside it. Settings stays as the other plugins have it.
- **Recording what you do** for a report (Settings → Making a report, `/gstudio record`, `mark`,
  `dump`, `save`).

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
