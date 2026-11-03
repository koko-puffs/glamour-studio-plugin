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
- [Asking about each character](#asking-about-each-character)
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
  your closet", Only looks I can wear today and the shopping list all read from it, for the
  character you view the site as. **It asks first**, on each character, whether to send it at all
  (see [Asking about each character](#asking-about-each-character)).
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
  plate they were meant for; one goes off the list once it's on a plate, or with "Done with these:
clear the list" under it) and, while the game's
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
  yours a look, three a minute, twenty a day). A shot grabbed after the HUD came back (Scroll Lock
  pressed meanwhile) says so under its preview. Watched (2560×1440, with DLSS on): the HUD hides and
  comes back, the picture goes up. A frame that never comes is given up on after five
  seconds and the HUD shown again.

### Plates

All twenty plates at once, five across: each its number, its pieces' icons, the look it is (or how
many pieces), a rose dot when one's planned for it on the site, and the jobs of the gearsets linked
to it; an empty one dashed. Above them **What you're wearing** (Try it on, Keep it as a look:
under the twenty plates it took scrolling to find), under them when the game last had them. The plate picked, in the pane beside: what's on it, its plan (a
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
read).

**Make room for me** (between the numbers and the lists; smaller in the dresser's panel, where
it's pressed twice): the plugin gathers the sets for you (`Game/OutfitStorer.cs`). The game stores
an outfit only from your bags or armoury chest, so for each set each loose piece is restored to
your bags (`MirageManager.RestorePrismBoxItem`, the dresser's "Restore item to its original form";
its place found again first, should the dresser close up), and once they're all there they go in
as the set (`StoreNewOutfit`, or `StoreExistingOutfit` into the set stored already, sending only
the pieces it lacks: `IsSetSlotUnlocked(place, column)` says which it has), a glamour prism a piece,
as storing by hand spends. The pieces sent are packed: first to last in the set's order, from the
first place on with no gaps, the rest `Invalid` (as GlamourLog's store-all does). A whole set's
pieces are then at their place in its list, which is why whole sets always went in; 0.6.1 sent the
sheet's columns (3 to 6) and 0.6.2 to 0.6.5 a partial set's places in the list (Gleaner's Attire at
1 to 3, a stored set's one missing piece at 1), and the game took each ask and never answered. The set's pieces
already in your bags go in with it. There are no set waits between steps: the game refuses an ask
(restore or store) while it's still settling the last one, and a refusal sends nothing, so a refused
ask is asked again every quarter second, for up to ten seconds, and it goes as fast as the game
takes them (recordings saw a refusal right after a piece came and right after a set was stored,
each taken a moment later; 0.6.3 gave up on a refused store). A store that gets no answer in 15 s
with nothing moved is asked once more. Until it has stored a set once here, a run does one set
(Try it on one set), so you can check it before it does them all. Before a set's first piece leaves the dresser, every piece is found again, your bags are
checked for room and a Unique piece you hold is left; each restore is waited for until the dresser
has one copy fewer and a new bag place holds it, the store until those places are empty, and a
stored set's place is found again before pieces go into it. A piece that doesn't come still lets
the ones that did go in as the set (when enough for one), then it stops and says which pieces are
left in your bags. It says first what it'll do:
the sets, the places freed, the prisms (and how many you have), the free bag places it needs at a
time; it's asked (Store them / Not now) and needs the dresser open. A new set needs two pieces or
more, loose or in your bags (a set's pieces left in your bags by a run that stopped are put away as
the set, freeing nothing but tidier than loose); pieces join a set stored already one or more at a
time. A set's pieces are all one quality, as the game's outfit window says ("Only items of the
same quality may be selected"): the quality with more loose pieces (a set left only because its
pieces are of both is counted and said). An undyed copy is used before a dyed one; a dyed one loses
its dye (storing takes it off, as the game's confirmation says; plates keep their own dyes), and
the ask says how many will. A piece two sets share goes to the first. A set the game doesn't take is left,
its pieces in your bags (said at the end), and the run goes on; three in a row and it stops. Stop waits for the step the game is answering. It's worked out again from the dresser as it
is when you say yes. Recordings write each step (`OUTFITS …`). New: not yet watched in the game.

**Duplicates** (the same card, and the dresser's panel): a second loose copy of an item with the
same dyes, and an undyed loose copy of a piece a stored set already holds (`DresserCleanup.Duplicates`;
copies dyed otherwise aren't duplicates). **Take out** restores them to your bags one at a time
(`Game/DresserTaker.cs`), each found again by item and dyes and waited for, never the last copy,
never a Unique piece you hold; it stops when your bags are full. Nothing's thrown away: they're
yours to sell or discard. Recordings write each step (`TAKE OUT …`). New: not yet watched.

## Beside the game's windows

A small Glamour Studio panel (300 wide, the plugin's own look) docked to the edge of the game window
you have open, its top level with the window's, moving with it as you drag it; on the side with room
(right by default; Settings can pin a side). As tall as what's in it, whatever the game window's
height (it measures what it drew each frame), scrolling only when the screen runs out. Its header: GLAMOUR STUDIO in small rose capitals over what it's beside, the arrow
that opens the plugin's window on the matching screen, and the fold button: folded, a slim tab stays
on the window's edge with its one number (looks ready, the plate, your list's length), remembered
for each window. The panels share their parts: a piece's row (its icon, name, a line under it, and
a chip for where it is: Dresser or Armoire in green, Bags, Armoury, Saddlebag or Retainer outlined,
Don't have in rose, On your list), a look's row (its four icons, name, a chip or a line, a small
button), headings with a count in words, "and N more" to show a list whole, and "Link your account
(Settings → Account)…" where linking's needed. It's gone with its window, while the HUD's hidden, in cutscenes and in group pose.
One panel at a time: the game window that has the game's focus, else the likeliest (Glamour Plates,
an outfit set, the dye window, the fitting room, the armoire, the dresser, Examine, a retainer, the
market board, a shop, in that order). It never opens the main window by itself; with the Glamour
Plates panel on, opening Glamour Plates doesn't open it either (Settings → Open with the plate editor
is off meanwhile). `UI/Companions/Companion.cs` is the panel, `Panes.cs` what each shows,
`Game/GameWindows.cs` the game windows' places and the few reads and presses only the panels need.

| Beside | It shows |
| --- | --- |
| **Glamour Dresser** | Places used of 800 as a bar, free, how many pieces could leave without breaking a look (and Make room); the looks the dresser and armoire have everything for (yours, the ones you love, and the ones sent to you), each with **Plate it**: a menu of the twenty plates (what each has on it, by look name, and what's planned for it; the plate planned for this look first, then the empty ones), and the one you pick is filled in one go (`UI/Views/PlateIt.cs`): it presses the dresser's own Glamour Plates button (`[u:5]`, as the first recording saw), picks the plate as clicking it does (`[i:17, u:plate−1]`, plate 1 to 14 and 1 to 3 in a recording), waits for the game to show it picked and 0.3 s more for its slots, and fills it as Fill plate does. Nothing's saved: you press Save in the game's window. It won't pick another plate over changes not yet saved. A look's own page has the same, **Put on a plate…**, while the dresser's open; your looks' pieces still in your bags, armoury chest, saddlebags or on retainers. |
| **Glamour Plates** (at a dresser) | Follows the plate picked in the game: its planned look (or one picked here: **Another look** lists the ready ones, then the rest of yours; a pick made here is for its plate), green when it's on the plate, its name opening it in the plugin; slot by slot, each marked On it, Changes, From the armoire, In your bags…, Don't have or Emptied; the dyes from your bags, the pieces to dye, and the missing bottles to put on your list; **Fill plate N**; then "Press Save in the game", and once saved Wear plate N. No plan: the ready looks, each with Fill with it. |
| **Glamour Plates** (from the Character window) | Every plate with something on it, by its look's name (or how many pieces), its linked gearsets' jobs, On for the one you wear, **Wear** for the rest (it picks the plate, `[i:17, u:plate−1]`, and once the game shows it picked presses the window's own apply, `[i:15, u:0]`, which applies the plate picked: 0.6.6 sent the plate's number with the apply and the window applied plate 1; nothing's applied if the plate isn't shown picked within two seconds. `/gearset change` is refused while this window's open, "…while occupied", a recording saw); the plate picked in the game highlighted. It fills no plate: that's the dresser's Glamour Plates. |
| **Armoire** | Your looks' pieces stored in it (plates take them from there); what in your bags and armoury chest it takes, each with **Store**, and **Store all** (one by one, 0.6 s apart); how many pieces in your dresser it would take (and which), each a place freed once moved. |
| **Storing an outfit set** | The set's name; dresser places freed by storing it (its loose pieces, less the one place the set takes); your looks that use it and which of its pieces; each piece and where it is. |
| **Fitting Room** | Your looks (all, or just the ready ones) to step through with the arrows, each put on in the fitting room (the panel stays while it reopens); where each piece is; the ones you don't have onto your shopping list; Wear it · plate N when the look's on a plate. What you tried on by hand: Keep it as a look. |
| **Examine** | How many of their pieces you have (a ring), each piece and where yours is, the missing ones onto your list, Try it on, Keep it as a look (named after them, "Their name's outfit", as Home's; "An outfit I saw" when the name can't be read). |
| **Dye** | The piece being dyed (the first dyeable gear in the window's values, else the one piece its preview shows: not yet watched), your looks that have it and the dyes each wants, the bottles in your bags for them, the missing ones onto your list. |
| **A retainer's bags** | Your looks' pieces on this retainer that aren't in your dresser or armoire; how much glamour on it no look uses, and what of it the armoire takes (and which); your other retainers, each with how many of your looks' pieces it has (or not read yet). |
| **Market board** | Your shopping list: what the market board sells (each with the look it's for, the cheapest listing while its results are open, **Search**, which types it into the market board's search and searches: not yet watched, else it copies the name), and what it doesn't (untradable). |
| **Shops** (`Shop`, `ShopExchangeItem`, `ShopExchangeCurrency`, `ShopExchangeCoin`, `InclusionShop`, `GrandCompanyExchange`, `FreeShop`, `SkyIslandExchange`) | What on your shopping list the shop sells, as far as its window's values name the items (best effort: a price or an icon that happens to equal an item's number would count too). |

**Under item tooltips:** while the game's item tooltip (`ItemDetail`) shows a piece of gear, a small
box under it (above, with no room below), GLAMOUR STUDIO over its lines: which of your looks have it, the plates it's on, on your
shopping list, and whether your dresser or armoire has it. Nothing when none of that's so.

**Right-click menu:** on gear in your bags, the armoury chest, a retainer's, an item linked in chat
(and the market board, recipes, shops), a **Glamour Studio** entry: Add it to a look… (your looks;
the piece goes in its slot, a ring on the free hand, `gs_plugin_look_put`), Which looks use it (a line in chat), Put it on the shopping list (when it isn't
yours), Store it in the armoire (from your bags, while the armoire's open).

Recordings write down which panel shows, where, folded or not, a look picked for a plate, and the
piece the dye window has (`PLUGIN … panel …`); and the market board's and shops' windows opening and closing, never their words (sellers' and retainers' names), as Examine's.

## Settings

**General**

| Setting | Default | What it does |
| --- | --- | --- |
| Empty the slots a look leaves bare | on | A look with no earrings empties the plate's earrings, so the plate is the look exactly. Weapons are left alone unless the look has one (a plate's weapon is its job's). |
| Dye pieces with the dyes in your bags | off | A piece whose copy is dyed otherwise than the look gets the look's dyes from bottles in your bags (the game spends them when the plate is saved). New, and not yet watched in the game: off, the plugin says which pieces to dye in the plate editor. |
| Show Glamour Studio beside the game's windows | on | The panels beside the game's windows (above). |
| Side | Auto | Which side of the game window a panel takes: the side with room, right or left. |
| Start folded | off | A window's panel you haven't opened or folded yourself starts as its tab. |
| Each window's panel | on | Glamour Dresser, Glamour Plates, Armoire, Storing outfit sets, Fitting Room, Examine, Dye, Plates from the Character window, Retainers' bags, Market board and shops: one by one. |
| Glamour lines under item tooltips | on | The box under a piece's tooltip. |
| Glamour Studio in the right-click menu | on | The right-click entry on gear. |
| Open with the plate editor | on | Opening Glamour Plates opens the window (off, and greyed out, while the Glamour Plates panel is on). |
| Keep it up to date by itself | on | Sends your closet as it changes (see [The site](#the-site)). |
| Send my plates and outfit | on | Your plates and what you wear go along, with your character's name and world (only you see them). |
| Say in chat when a wish comes true | on | A line in chat when pieces on your shopping list are seen in your wardrobe (they come off the list either way). |
| Forget this character | | Forgets what the plugin has read of the character you're playing (it's read again as you play). |

One column in the middle (640 at most), as the other plugins' Settings: General and Account as a
pick-one pill, each group a label over a card of rows.

**Account:** linked or not in a line, the token (masked once linked, with Unlink; else a field and
Link), Get a token (the site's Settings → Glamour Studio → In-game plugin) and Open Glamour Studio.
Then **Characters**: the one you're on first, then each you've answered for, by name, each a switch
(Send to the site) with its world and where it stands (Sent to the site, Not sent, Not asked yet, Yes
· telling the site, Checking with the site…, "you're on it"); greyed out while not linked. What's
shown of each on the site is the site's Settings → Glamour Studio → Characters. The version and
What's new at the foot.

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
emptied with `ClearItemData`. Dyes from your bags go on with `SetItemStain` (the slot, the dye,
the bottle's inventory item and the channel, as dragging a dye onto the slot does). Before 0.4.1 it
was `SetSelectedItemStains`, which dyes the slot the editor's own menu has picked, not the one just
filled: a recording saw every dye of a look land on the weapon, the last one winning, and nothing
else dyed. The slot picked before is put back. The
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
whose gear the plate fits. Watched: it changes to the gearset and projects the plate in an inn and
in a city ("Unable to fully apply…" is the game's for pieces your job or character can't wear).
Out in a field the game changes gearset but says "Unable to apply glamour plates here"; in combat
it refuses ("Unable to execute command during combat"); at a glamour dresser, or with Glamour
Plates opened there, and with Glamour Plate opened from the Character window, "Unable to execute
command while occupied". So Wear and a gearset's Change
to it (`Gearsets.Blocked`) are greyed out, the reason their tooltip, in combat, at the dresser or
in either plate window (from the Character window the panel's Wear uses that window's apply
instead), while loading or busy (an event, a bell, casting), and Wear outside a
sanctuary (Change to it there says the plate waits for one). The game also sets the gearset's
instant portrait, as changing gearset always does (and says so when the portrait doesn't fit).

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

The third batch (0.3.3): the dresser read at once as pieces go in, come out, and an outfit set is
stored (its pieces leave your bags, the set takes one place); the game stores a set with a piece
missing too ("Includes items that can be stored in an armoire"), the rest added later. Storing a
set (`MiragePrismPrismSetConvert`): the agent's source is the piece it was opened from (0 when
adding to a set stored already), each piece listed with the bag it's put from (`Invalid` until
picked), so the panel works the set out from its pieces. Gearsets: linking and unlinking a plate
from the plugin shows in the game's Gear Set list and lasts through logging out; changing gearset
behaves as Wear's above.

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
| `gs_plugin_characters` | linking, changing character, every 10 min while the window's open (30 otherwise) | what the site does with each of your characters (key, `ask` / `yes` / `no`) |
| `gs_plugin_character` | you answer the question, or flip a character's switch in Settings → Account; an answer the site hasn't heard yet (a minute after a failed try) | your answer for a character: yes with its name and world; no with its key alone, and the site lets go of what it had of it |
| `gs_plugin_sync` | your wardrobe changed and settled for 10 s, at most every 3 min (and only when what's sent changed); Send it now; saying yes; linking. **Only for a character the site takes (`yes`)** | the character you're on (key, name, world), its pieces each where they are, the places read in full, its plates and what it wears |
| `gs_plugin_looks` | the window opens, every 10 min while it's open, Refresh | your looks (300), the public ones you love (200), your shopping list's items |
| `gs_plugin_inbox_list` | as the window opens (any way: the plugin list, `/gstudio`, a panel's arrow), every minute while it's open, every 15 otherwise | the looks sent to the game, each whole, with the plate meant |
| `gs_plugin_inbox_remove` | a sent look went on a plate | it's off the list |
| `gs_plugin_save_look` | Keep it as a look (a plate, your outfit, someone examined) | a new private look of yours |
| `gs_plugin_plans` | the window opens and with the looks, and when you change character (30 s at most); only for a character the site takes | that character's plate plans, each look whole |
| `gs_plugin_wearable` | the window opens, every 10 min while it's open, after a sync that changed the closet, on another character | public looks you own every piece of, the most loved first: as the character you're on (`p_character`) when the site takes it, else with all your characters' pieces |
| `gs_plugin_wish_add` | a panel's "Add … to the shopping list", the right-click menu's | up to 40 pieces (item and dyes) onto your shopping list; a piece on it already stays |
| `gs_plugin_look_put` | the right-click menu's Add it to a look | the piece into one slot of one of your own looks |
| `gs-plugin-shot` (edge function) | Send it to the look | multipart: token, look, caption, the JPEG; the site checks the token, the look (public), the limits, and stores the shot |
| `gs_plugin_shot_character` | right after a shot's sent, for a character the site takes | which character wore it (the site shows it on the shot only while you show that character on your shots) |

`gs_plugin_sync`'s answer has `wishes_got`: the items it took off your shopping list because this
sync had them.

On the site, each character's reads are kept apart (`gs_closet_reads`), and the plugin's pieces in
your closet are made again from all of them, each in its handiest place. So another PC, or one
character played alone, never takes another character's pieces off; pieces you ticked by hand stay
yours until the plugin sees them somewhere. Plates and what
you wear are kept per character (eight at most). The site shows a closet per character too (its
Viewing as switch), and its Settings → Glamour Studio → Characters lists them: whether it takes each
one's data, what's public of each (nothing until you say), and Forget.

## Asking about each character

A character's wardrobe goes to the site only once you've said yes to it (`Core/Consent.cs`, tested;
`Session/SiteSync.cs`). Your answer is kept per character in the plugin's settings (`Characters`, by
its key, with its name and world to list it) and on the site (`gs_game_characters.send`), which turns
away a character you said no to whatever plugin sends it. The site's word wins, since you can change
it there too: what to do (`Consent.Decide`) is

| Here | The site | So |
| --- | --- | --- |
| anything | yes | send |
| anything | no | nothing sent (your answer here becomes no) |
| yes | not said (`ask`, or it's never heard of it) | tell it yes, then send |
| no | not said | tell it no (it lets go of anything an older plugin sent) |
| not asked | not said | **ask you**, sending nothing |
| no | not read yet | nothing sent |
| yes or not asked | not read yet | wait (nothing sent) |

**The question** (`UI/Views/Characters.cs`): while linked, on a character that needs asking, a card
of its own at the top of the screen (440 wide, the question's card as the window has it: the rose
edge, an eyebrow "A new character on Lich", "SEND KOKO NYAN'S WARDROBE TO GLAMOUR STUDIO?" in Syne,
what goes, that it stays private, and that nothing is sent until you say yes), whether the window's
open or not; gone in cutscenes and group pose and while the HUD's hidden, as the panels are. **Yes,
send it** (rose): told to the site, then the closet goes at once, its plans and Wear now read as that
character; **No**: told to the site, which lets go of what it had of it; **Not now**: not asked again
about it until the plugin loads again (Send now in Closet asks again). A line in chat says what you
chose. Not linked, nothing's asked (nothing could be sent); on linking it is. The header's pill and the
rail's line say "This character isn't sent yet: say yes or no" or "isn't sent to the site".

A sync the site refuses for a character you said no to there (hint `refused`) isn't shown as a
problem: the character's answer here becomes no. Older plugins (0.4.x and before) never ask: the site
keeps what they send of a character no one's answered for (private), and refuses one that's no, with
words saying to update the plugin.

## The code

| Folder | What |
| --- | --- |
| `Plugin.cs`, `Services.cs`, `Configuration.cs` | Loading, the command, the framework tick (reading, the site, opening with the plate editor), settings. |
| `Core/` | Game-free, tested: slots and the game's orders (`Slots`), looks and their JSON (`Looks`), the wardrobe and what's sent (`Wardrobe`), asking about each character (`Consent`), the plan for a plate (`PlatePlan`), how ready a look is (`Readiness`), the site's calls (`SiteLink`), words (`Words`). |
| `Game/` | The game: its sheets (`GameData`: items, dyes, armoire rows, outfit sets, jobs), reading your wardrobe (`WardrobeReader`), the plate editor, try-on and wearing a plate (`PlateEditor`), the armoire (`Armoire`), gearsets (`Gearsets`), the Examine window (`Examine`), the recorder (`Recorder`, `AtkText`). |
| `Session/` | `SiteSync`: the site in the background. `Shots`: taking, making and sending a shot. |
| `UI/Companions/` | The panels beside the game's windows (`Companion`: docking, folding, the header; `Panes`: what each shows; `ItemExtras`: the tooltip lines and the right-click menu). `Game/GameWindows` holds their game side. |
| `UI/` | `MainWindow` (the header, the rail, the screens) and its screens (`Views/Home`, `LooksView`, `LookView`, `PlateView`, `ClosetView`, `CleanupView`, `Settings`, `Characters` (the question, and Settings' characters), `Shared`); `Kit/` the controls, colours, fonts and icons (`Layout`: panes, the rail's items, tabs with counts, chips, the ring); `Model/` the kit's game-free parts (SVG paths). |
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

### 0.6.10

- The right-click menu's **Try it on with …** is gone: it did nothing when chosen.

### 0.6.9

- **Plate it** puts a look on a plate in one go: pick the plate from a menu (what each plate has
  on it, and what's planned for it), and the plugin opens Glamour Plates, picks that plate and
  fills it. You press Save. Also on a look's page as **Put on a plate…** while the dresser's open.
- Ready for a plate lists the looks sent to you too, and up to twenty.

### 0.6.8

- High-quality pieces go into sets (0.6.5 kept them out after the game refused Bouncer's Attire;
  the game takes them, all of one quality: "Only items of the same quality may be selected"). A set
  is gathered in the quality with more loose pieces.
- Dyed pieces go into sets too, an undyed copy first: storing takes a dye off (the game's own
  confirmation says so), and the ask says how many dyed pieces will lose it.

### 0.6.7

- **Wear** in the panel beside the Character window's Glamour Plate applied plate 1 whatever the
  plate: the window's apply applies the plate picked in it, and its number isn't the plate. Now the
  plate's picked first (`[i:17, u:plate−1]`, as clicking it does), then applied.

### 0.6.6

From the 0.6.5 run, and how GlamourLog's store-all sends sets:

- The pieces sent to store a set are packed from the first place, in the set's order, with no
  gaps. Sending a partial set's pieces at their place in its list left a gap where a missing piece
  goes, and the game took the ask and never answered (Gleaner's Attire, #55 Armor, Mistwake's one
  piece into its stored set). So a new set needn't be whole after all: 0.6.5's rule is undone.
- Into a set stored already, only the pieces it lacks are sent: 0.6.5 sent a copy from your bags
  of a piece it had (Southern Seas Shirt & Trousers), and the game refused.
- A set the game doesn't take no longer ends the run: its pieces stay in your bags, said at the
  end, and it goes on (three in a row, and it stops).
- A set's pieces in your bags (two or more, from a run that stopped) are put away as the set.

### 0.6.5

From the 0.6.4 run:

- A new outfit set is made only whole. Every set that hung ("the game hasn't answered", Gleaner's
  Attire, #55 Armor) was missing a piece: the game takes the ask and never answers. Every whole set
  went in at once. Sets missing a piece aren't planned any more (Make room's list too).
- High-quality pieces stay out of sets: the game refused Bouncer's Attire's (high quality) every
  time. A set that waits only on a dyed or high-quality copy is counted and said.
- A set's pieces already in your bags count towards it (normal quality only), and their prisms are
  counted too.

### 0.6.4

From a long Make room for me run (95 sets down to 51, then two stops):

- Faster: no set waits between pieces and sets. The game refuses an ask while it settles the last
  one (a refusal sends nothing), so a refused restore or store is asked again every quarter second,
  up to ten seconds, and it goes as fast as the game takes them. Take out duplicates too.
- A store the game refused stopped the run ("wouldn't store … (glamour prisms?)", with 451
  prisms): now it's asked again like a restore, and prisms are only blamed when you're short.
- A store with no answer waits 15 s (was 8), counts as done when the new set shows in the dresser,
  and when nothing moved is asked once more; if it's still not taken, it says the pieces are in your
  bags rather than "may have gone into the set".

### 0.6.3

From Make room for me's first full run (it works: sets stored, a prism a piece):

- Two seconds between sets: a set's first piece asked for a second after the last set was stored
  was refused every time (then taken on the second ask).
- The dresser's panel says the free bag places it needs, as Make room does.
- Try it on one set was for the first run only: once a set's been stored, the button does them all.

### 0.6.2

From the first try of Make room for me:

- Storing a set sent each piece at the wrong place (its slot's column, not its place in the set's
  list), and the game took nothing: the pieces stayed in your bags. Now in the set's order.
- A restore the game refuses is asked again (it refused the fifth piece, and once the second,
  with room in the bags); pieces go a little slower.
- A set's pieces already in your bags (left by a run that stopped) go in with it.
- Until it has worked once, Make room for me does one set (Try it on one set).
- Take out duplicates asks again too, and goes at the same pace.

### 0.6.1

- **Take out duplicates:** on Make room and in the dresser's panel, the dresser's duplicates (a
  second copy with the same dyes, or an undyed copy of a piece a stored set holds) come out to your
  bags, to sell or discard. Asked first; never the last copy.
- Make room for me, made safer before it's first used: a piece landing in your bags' very first
  place was never seen arrive; every piece, your bag room and Unique pieces are checked before a
  set's first piece leaves; a piece that doesn't come still lets the others go in as the set; a
  stored set's place is found again before storing into it; bag places are checked again before
  storing; glamour prisms are found on every client language; Stop waits for the step under way;
  it's worked out again when you say yes, and can't start twice.
- Make room scrolls when Make room for me pushes its lists down; its buttons go under their words
  in a narrow window.

### 0.6.0

- **Make room for me:** on Make room and in the glamour dresser's panel, the plugin gathers outfit
  sets' loose pieces into their sets for you (restored to your bags, stored back as the set, a
  glamour prism a piece). It says what it'll do and asks first; dyed pieces stay loose. New: not
  yet watched in the game.
- The panels beside the game's windows are as tall as what's in them (they were held to the game
  window's height, and measured a little short, so a scrollbar showed that barely moved).
- Keep it as a look from the Examine panel names the look after them ("Their name's outfit"), as
  Home does; the panel's title says whose outfit it is.
- Fitting: a heading whose label doesn't fit beside it puts the label under it (Gearsets showed
  only its label), the rail keeps "Make room" whole (its count shorter, or in the tooltip), and a
  few lines that could run past their card are cut to fit.

### 0.5.2

From the recordings of sections E, F, G and I:

- **Sent from the site** shows what you've just sent: the plugin reads it as its window opens,
  however it's opened (opened by `/gstudio` or a panel's arrow, it waited up to 15 minutes and showed
  the last look sent instead). "Done with these" clears looks sent and never put on a plate.
- **Wear** in the panel beside Glamour Plate (opened from the Character window) works: it presses
  that window's own apply (the game refuses `/gearset change` while it's open). Elsewhere, Wear and
  Change to it are greyed out while that window's open.
- A shot taken while the HUD came back (Scroll Lock pressed meanwhile) says it's in the shot.
- **What you're wearing** sits above the twenty plates on Plates, not under them.

### 0.5.1

- **It asks before sending a character.** On a character it hasn't seen (linked), a small card at the
  top of the screen asks whether to send its wardrobe to Glamour Studio; nothing of it is sent until
  you say yes. No: the site lets go of what it had of it. Not now: asked again next time.
- **Settings → Account → Characters**: each character you've answered for, with its switch.
- The site keeps your answer too, and you can change it there (Settings → Glamour Studio →
  Characters, where you also choose what's public of each: nothing until you say). What you change
  on the site is followed here.
- **Wear now** is what the character you're on can wear (when the site takes it), not every
  character's pieces together.
- A shot you send says which character wore it (shown on the site only if you show that character on
  your shots).

### 0.5.0

- 0.4.2's changes, released as 0.5.0 (nothing else).

### 0.4.2

- The panels beside the game's windows and the box under item tooltips lose the GS mark (the small
  GLAMOUR STUDIO label stays), and say things the same way in each: where a piece is, the counts
  in their headings, "and N more" (each list's own), linking.

### 0.4.1

From the third batch of recordings:

- **Dyes from your bags** go on the right pieces (they all went to the weapon, and only the last
  one stuck).
- **Wear** and **Change to it** are greyed out where the game says no, the reason in their
  tooltip: in combat, at the glamour dresser or its Glamour Plates, and (Wear) outside a
  sanctuary. Glamour Plates' panel no longer offers Wear right after saving (the dresser's open):
  it says where to wear it.
- The outfit set panel names the set being stored (it took the piece it was opened from for it).

### 0.4.0

- **Beside the game's windows:** a small Glamour Studio panel docked to the glamour dresser, Glamour
  Plates (at a dresser, and from the Character window), the armoire, storing an outfit set, the
  fitting room, Examine, the dye window, a retainer's bags, the market board and shops, moving with
  the window. Each shows what the plugin knows for that window and does what it can there (Fill
  plate N, Plate it, Store, Wear, step through your looks in the fitting room, search the market
  board for your shopping list…). Fold one to a tab; turn each on or off in Settings. The main
  window stays closed (it no longer opens with Glamour Plates while that panel's on).
- Lines under item tooltips: which of your looks have a piece, its plates, your shopping list.
- A Glamour Studio entry in the right-click menu on gear: add it to a look, try it on with a look,
  which looks use it, put it on your shopping list, store it in the armoire.
- Pieces go on your shopping list from the panels (Examine, the fitting room, dyes).
- Not yet watched in the game: the dresser's Glamour Plates button pressed from the panel, the
  piece the dye window has, typing into the market board's search, what a shop sells.

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
