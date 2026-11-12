# Glamour Studio plugin: the details

The full reference: every screen and setting, how a plate is filled, what the plugin reads and
sends, how the code is laid out, releasing, and the release notes. The short version is the
[README](README.md).

- [What it does](#what-it-does)
- [The window](#the-window): [Home](#home), [Looks](#looks), [A look](#a-look),
  [Plates](#plates), [Closet](#closet), [Make room](#make-room)
- [Beside the game's windows](#beside-the-games-windows)
- [Settings](#settings)
- [Making a report: the recorder](#making-a-report-the-recorder)
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
  your closet", Only looks I can wear today and your wishlist all read from it, for the
  character you view the site as. **It asks first**, on each character, whether to send it at all
  (see [Asking about each character](#asking-about-each-character)).
- **Both ways.** Looks you Send to game on the site wait in the plugin (Sent from the site); a
  plate or someone you examine can be kept as a new private look on the site (Keep it as a look),
  and what you wear too (Copy my game outfit, the site's words for it); the dressing room's Copy my
  outfit from the game reads what it sent.
- **Plates planned on the site.** On the site's Closet → Plates, pick a look for each plate of a
  character; at a dresser, Home and the panel beside Glamour Plates offer Fill plate N with that look.
- **Wear today.** The public looks on the site you own every piece of, the most loved first.
- **Your wishlist ticks itself.** A piece on your wishlist that the plugin sees in your wardrobe
  comes off it on the site (and a line in chat says so, if you like).
- **Make room in your dresser.** How full it is, the outfit sets whose loose pieces could be kept
  as one outfit, the duplicates, and the pieces none of your looks or plates uses.
- **Put away.** The pieces a look needs that are in your bags or armoury chest, and with the armoire
  open, Into the armoire for the ones it takes.
- **Gearsets.** A plate's linked gearsets, Link to a gearset, Unlink, Change to it.
- **Shots for the site.** Take a shot of a public look as worn (the HUD hides for a moment), see it,
  caption it and send it to the look's Worn in game.
- **Examine.** While the game's Examine window is open, the outfit you're looking at: Try it on, or
  Keep it as a look of yours.

`/gstudio` opens the window, `/gstudio settings` opens Settings, `/gstudio send` sends your
closet now, `/gstudio refresh` reads it all again (as the Refresh button does).

**Refresh** (the round arrows beside Settings at the right of the window's bar, and in each panel's
header beside the game's windows): your looks, wishlist, plans, the looks sent to you and what you
can wear are read from the site again, and your wardrobe from the game. The dresser and armoire are
read only while they're open (the game has them only then), so they're as of when you last opened
them. The site's looks are also read every ten minutes while the window or a panel is open.

## The window

Since 0.7.0 the window, the panels beside the game's windows and the question about a character
are in the site's **Character Sheet** look (the site's redesign of 11 November 2026), built to the
mock-up in [docs/redesign](docs/redesign/README.md) (a deck and a page per screen, with notes). It's
built now, so the mock-up is the record of the design; where the two differ, this page says what the
code does. (Until then 0.6.16's "Moonlit Atelier": a violet night with mint and orchid, Outfit for
titles, a rail down the left.)

**Colours** (`UI/Kit/Palette.cs`, the mock's names). Two grounds. The *ink* is the studio: the
window near-black violet (`#0c0a12`), Dalamud's title bar and the side panes a step up, cards and
callouts a step more, opaque lilac-grey hairlines (the strong one round what you press or type in,
and round popups); words in Ink, Body, Muted and Faint. The *field* is a screen's one moment: flat
periwinkle (`#8f7cf6`), its floor deeper, its page dotted every 14px, and on it words, outlines,
tracks and the sticker turn ink-violet (`Kit.FieldPanel`, `Tint`). Lilac (`#c9b8ff`) for where you
are, eyebrows, links and the hand voice. Three honest colours for what you have: owned **ice**
(`#8ce2ff`: in the dresser or armoire, done, sent, linked), missing **pink**, on your wishlist
**lavender** (with ★); **amber** for what to see to (a piece in your bags, a dye to do, a button
waiting for its second press, a warning); red only for what went wrong. *Holo* (a marble texture,
sliding slowly; still under reduced motion) means special: the underline under where you are, the
sparkle for good news and something new.

**Type** (`UI/Kit/Fonts.cs`, the faces in `Resources/Fonts` with their SIL Open Font Licences):

- **Bricolage Grotesque** ExtraBold (800), always in capitals, for what a place or a look is: titles,
  headings, a look's name, the wordmark, big numbers (10 to 60px). At 72 and 104 it's the *echo* (a
  name huge behind a field), its atlas only A–Z, 0–9 and a few signs (a name with none of them, in
  kana say, has no echo).
- **Shantell Sans** (560), the hand-lettered voice that says one thing to you ("you press Save in
  the game", "empty", "a wish came true!"), at 13, 15 and 18.
- **Figtree** 400 to 700 to read, for buttons, labels and eyebrows (600, eyebrows in spaced
  capitals), and 800 for the sticker's words.
- **Geist Mono** for numbers that change, lined up (counts, a token, a gearset's job and number).
- **Noto Sans CJK** Medium (Dalamud's own copy) as the fallback: names can be in any script. A
  character a face hasn't got is drawn by `Txt` in Noto at the nearest of its sizes (11, 13, 15, 18,
  24, 34), scaled to the size asked; one neither has yet is left out for a moment and asked for
  (`Fonts.Want`): the atlas is built again with it at most once a second, counted from when the last
  build ended (never while one's running), the Latin ones (Ł, ő, ș) into the faces too, up to 6,000
  characters in all. The atlas grows only by what's shown. So no name shows ImGui's "?".

Each is a static instance of the variable font cut to Latin (Bricolage at 800 and its 96pt optical
size, as the site fixes it). Sizes are CSS sizes loaded through Dalamud's font atlas (so they follow
its global scale and rebuild with it), each at its em times its line, so 14px here is as tall as in
the browser. On a Japanese client the Figtree sizes are Dalamud's own font (it has the kana and
kanji item names need).

**Textures** (`Resources/Textures`, embedded; made once with the fonts and let go with them,
`UI/Kit/Textures.cs`): the logo (the site's clothes tag with its hanger) at 32, 64, 128 and 256px,
the smallest that's big enough drawn at 18 to 96; `holo.png`, a 256px tile of the holo marble;
`spark.png`, the holo four-point sparkle; `cloud.png`, white, tinted, behind an empty page. Until a
texture's made the part draws without it (lilac for holo). The game's item icons are the game's.

**Parts** (`UI/Kit`): the **sticker** is a screen's one thing to do (Fill plate 3, Put on plate 3,
Try it on, Wear it, Make room for me, Yes, send it, Link): periwinkle with ink words, a 2px edge and
a hard 3px block down-right; pointed at, lighter; pressed, it moves into its block; on the field it
turns over (ink with white words, a black edge and block); disabled it stays a sticker at half
strength, with why as its tooltip. Everything else you press is outlined (the strong edge, lilac when
pointed at) or quiet. A **callout** is an ink card on the field with a 2px black edge and a 4px black
block (washed pink for a piece you haven't got); the **plate slip** is a ticket (notches cut at its
sides, a dashed tear); the maker's **seal** their initials in a stamp turned −4°; the **sign** an
empty page's title, a white card turned −5° with the cloud behind and a hand-lettered line under it.
Tags (6px corners) in the honest colours; item tiles with a corner dot for where the piece is; the
dyes as a palette strip; the readiness ring; the bar (ice, periwinkle while working, amber when
nearly full).

The window is 1120 × 760 to start with (fitted to 90% of the screen the first time), 900 × 600 at
the least. Dalamud's title bar (▼, ☰, ×) stays Dalamud's.

**The bar** (56px under the title bar, its hairline at the foot; `MainWindow.DrawBar`,
`UI/Kit/Bar.cs`): the logo and GLAMOUR STUDIO, a slash, then the five places, **Home · Looks ·
Plates · Closet · Make room**, each its icon, name and count (your looks; plates with something on
them, of 20; the pieces you have; the dresser's places used, of 800), muted; the one you're on in
Ink with a lilac icon and count and the holo underline over the bar's line. At the right **your
character's pill**: their initials on a periwinkle face (grey when not linked or not sent), a dot for
how it's going (breathing while something's going on, ringed while recording) and one line, the
first that's true of:

| The pill says | Its colour |
| --- | --- |
| Recording what you do · 1,204 lines | red, ringed |
| Plate editor open · plate 3 | lilac, breathing |
| Not linked to Glamour Studio | grey |
| Link again: the site doesn't know this token | red |
| Sending your closet… | lilac, breathing |
| Closet not sent: see Closet (what went wrong is said in full there) | red |
| This character isn't sent yet: say yes or no | grey |
| This character isn't sent to the site | grey |
| Yes · telling the site… (your answer stands until the site has it) | lilac, breathing |
| Glamour Studio couldn't ask about this character (the site couldn't be reached for its answer; asked again a minute later) | red |
| Closet up to date · sent 4 min ago | ice |
| Linked · sends your closet as you play ("Linked", with sending by itself off) | ice |

With no character logged in, "Log in to a character". Pointed at, the pill shows the character's
name and world, and under them its line in full when it was cut to fit; pressed, it opens Settings → Account. Then **Refresh** (dimmed, its arrows turning,
while it reads) and **Settings** (the sliders), quiet and round. When they don't all fit (under 1000
wide, or long counts) the counts go first, then the places' words, the wordmark and the pill's line,
each to a tooltip (`Model.BarFit`). A look and Settings are screens below: the bar has the back
arrow at the left (to where you came from), the screen's eyebrow and title, and on a look On the
site, Refresh and Settings.

Each screen pads itself (20 above, 24 at the sides) and has one field and one sticker at most; most
are a list or grid with the thing picked in a side pane on the second ground.

- **Toasts** rise in over the window's foot (28px up) for four seconds (`Frame`): good news with the
  holo sparkle; a warning or a failure amber with the warning sign (never the sparkle).
- **A question** (Make room for me, taking out duplicates, Forget this character, Unlink): a card
  over the window (440 wide, 460 with a list; 28px corners, the strong hairline) that darkens and
  takes every click on the plugin's window only: an eyebrow, the title in display capitals, what
  it does, what it'll do as lines with a dot in the honest colours, a muted note, then No and the yes
  (the sticker; a danger question's yes filled red). Escape is No. A new question is laid unseen for
  a frame to measure it. It goes when the window closes or the character changes.
- **A screen that fails** says "This screen failed (see /xllog)" where it would be, in a card with the
  danger edge (logged once, not every frame); a press whose action fails, "Something went wrong (see
  /xllog)".
- **Tooltips** are raised ink with the strong hairline and 10px corners (260 wide at most; a piece's:
  its name, its slot and dyes, and where it is in its colour); **menus** and combos' popups raised
  ink with 16px corners. A disabled button's tooltip says why.
- **Numbers** are written in the invariant culture ("1,284" and "4:12"), whatever Windows'
  language is.

### Home

**At the glamour dresser** (Glamour Plates open there), the field: the eyebrow "Plate 3 · open in
the game" (· not saved yet, with changes), PLATE 3 echoed behind, and in big stacked capitals the
look planned for that plate on the site, "Planned for this plate on the site · by its maker · 7
pieces", tags for what filling does (5 go on, 1 from the armoire, 1 to put away first, 1 missing, 2
dyes from your bags, 1 to dye in the editor, 3 slots emptied), the **Fill plate 3** sticker (greyed
when none of its pieces is in the dresser or armoire), See the look, and the hand "you press Save in
the game". At its right a callout of the plate's twelve slots, **Now** and **After**: each slot's
corner dot ice from the dresser, amber from the armoire or with a dye to see to, pink when the look's
piece isn't there (the slot's kept); × emptied; faded when kept as it is; a note (weapon kept · bare
slots emptied) and what the dots mean. Pointed at, a slot says its piece, its dyes and why it's kept.
With the plan on the plate already: "It has the look planned for it on the site". No plan: the look
it is (or Plate 3) and "Pick a look below to put it on". After filling: "Plate 3 · filled · not saved
yet" (or saved), what went on ("All seven went on, two dyed Snow White from your bags. One slot
emptied."), the hand "now press Save in the game's window" (or "saved: close the plate windows to
wear it"), Fill it again and See the look, and at the right a callout with a ring of how many went
on, ON PLATE 3 and how to wear it (close Glamour Plates and the dresser, then Wear plate 3 in Plates:
the game won't change gearset while they're open).

**Away from a dresser** (linked): a quiet card, "At a glamour dresser: open Glamour Plates and pick a
plate: it shows here with the look planned for it, ready to fill", that every inn room has one, the
plates you've planned on the site (by number, up to four), and Plan plates on the site.

**Not linked** (the first run): the field, "Three steps · once", LINK YOUR GAME, what linking brings,
the three steps as callouts (link your account, open your dresser and armoire once, put a look on a
plate), the **Link your account** sticker (to Settings → Account) and Open Glamour Studio. With the
plate editor open it's a plain card under the plate's field.

Under it, two columns (the right one 356 wide):

- **Ready for plate 3** (Ready for a plate, away from one), its count ("6 of 14") and All your looks
  (Looks, Ready first on): up to six looks as small character sheets, three across (two when
  narrow): its pieces on a raised well with their corner dots, its name in capitals and a line
  (Planned for this plate, Planned for plate 5, Your look, Loved · by its maker, Sent · by its maker),
  the maker's seal, how ready it is, and its dyes as a palette strip along its foot. The look planned
  for the plate open first (away from one, the ready ones planned for any plate first), then yours,
  the ones you love and the ones sent to you whose every piece is in the dresser or armoire. Pressed,
  it opens. None: "None ready yet"; your looks not read yet, the hand "reading your looks…"; not
  linked, the empty page "No looks yet".
- **Examining**, while the game's Examine window shows someone: "Their name's outfit", "Glamour and
  dyes as shown", their pieces (each yours or not yours yet), Try it on and Keep it as a look (a
  private look named "Their name's outfit", or "An outfit I saw" when the name can't be read, their
  body for the mannequin). Someone new shows once two reads half a second apart agree.
- **Sent from the site**: the looks you sent from the site, newest first ("For plate 3 · sent 4 min
  ago"; the sparkle before one Home hasn't shown you before). Pressed, it opens. One goes off the
  list once it's on a plate, or with "Done with these: clear the list" under them.
- **Wear today**, its count and All (Looks' Wear today): four of them as rows with their loves (two
  when looks were sent; none while you're examining someone).
- Not linked: **Already here**, what works without an account (Try it on, Make room, Your plates),
  each opening its screen.

### Looks

A list down the left (548 wide; 420 under 1000, its words shorter) and the look picked beside it on
the second ground.

- **Tabs** underlined, with counts and the holo underline: **Your looks**, **Loved**, **Wear today**
  (the public looks on the site you own every piece of: `gs_plugin_wearable`, 60 at most, the most
  loved first, read when the window opens, every 10 minutes and after a sync that changed your
  closet). Each count shows once your looks have been read. Another tab picks its first look and lets go of the job and mood.
- **A search** (name, maker, job, mood) with a × that empties it (Escape while typing empties it too),
  **Ready first** (on by default; not on Wear today, whose looks are all yours), a job and a mood (the
  ones the tab's looks say). Too narrow for one row, they go under the search.
- **Each row** (64 tall): four of its pieces, its name in display capitals, whose it is and what for
  (planned or sent for plate N), and how ready it is at the right: Ready for a plate (ice ✓), so many
  to store (amber), so many of so many yours, None yours yet (pink), Nothing on it; on Wear today, its
  loves (a pink heart). The one picked lilac-washed.
- Empty: No looks yet (Open the dressing room), No loved looks yet (Open Discover), None yet (Wear
  today); nothing found: "No look matches “gold saucer” for a healer."; reading: the rows' shapes
  breathing and "reading your looks…"; not linked: "Your looks show here once you're linked"; a
  token the site doesn't know: Link again, with Settings → Account.
- **The look picked**: a small character sheet, its name on the field with its echo, its eyebrow
  (Your look · planned for plate 3, Loved · by its maker, Wear today · by its maker · you own it all),
  the maker's seal and its dyes as a palette strip (six at most; or Undyed); Try it on (the sticker, unless the
  plate's part has one), Shot (public looks: opens the look for a shot), Site and Open the look (icons
  alone when narrow). Then the plate: with Glamour Plates open, the plate slip with **Put on plate N**
  (as a look's, below); at the dresser, Put on a plate…; just filled, what went on; else a line saying
  how ready it is and how to get it on a plate. Then **Pieces**: each its icon with its corner dot,
  name, slot and dyes, and where it is: Dresser or Armoire (ice ✓), In your bags, In your armoury
  chest… (amber: put it away first), On your wishlist (lavender ★), Missing (pink).

### A look

Opened from a list, Home, Plates or a panel's arrow; the bar has the back arrow (to where it came
from), its eyebrow and name, and On the site. At the left its **sheet** on the field (24 in, 18 above
and under), the name echoed behind: its pieces as callouts round the middle in the game's gear
window's order (weapon, head, body, hands, legs, feet down the left; off hand, ears, neck, wrists and
rings down the right), each its icon with its corner dot, name, slot, its dyes as swatches (an empty
ring for a channel left undyed) and where it is as a tag: ice where a plate takes it from (amber with
a dye to see to), amber when it's elsewhere of yours, lavender on your wishlist, pink missing (washed
pink, its icon faded); pointed at, a tooltip with all of it. A slot the look leaves bare is a dashed
outline: — kept, or × emptied on the plate. In the middle: the big ring (how many pieces are ready for
a plate), Ready for a plate, Not ready yet or Nothing on it, what stands between it and a plate (a
small callout), its palette strip (its dyes, six at most) and **Try it on** (the sticker when the slip hasn't one; any piece,
yours or not).

At the right, the side (348 wide, 300 under 1000):

- **The plate slip** (a ticket). With the plate editor open: "Take it into the game", PLATE 3 and a
  tag (Open now, Sent for this plate, or Sent for plate 5 in amber: pick plate 5 in the game's
  window), then, past the tear, what it'll do as dotted lines ("All seven go on: six from the
  dresser, one from the armoire", what's elsewhere or not yours, what's dyed from your bags, what to
  dye in the editor, bare slots emptied), **Put on plate 3** (the sticker) and "nothing's saved till
  you press Save in the game". At the dresser, Glamour Plates not open: **Put on a plate…** and Plate
  it's menu (below). Just filled: "Filled: press Save in the game" (Filled, once saved), what went
  on, **Wear plate 3** (the sticker, once saved; greyed with why where the game says no: see Wear it)
  and Fill it again. Closed: which plate it's for when it's ready, how to get there, and Add the
  missing pieces to your wishlist. A filled plate is let go (and its Wear plate N) when the plate's
  changed by hand since, left with changes not saved, or saved otherwise.
- **Put away first** (pieces that are yours but not in the dresser or armoire): each with where it
  is and whether the armoire takes it. With the armoire's window open (at an inn), **Into the
  armoire** stores one from your bags or armoury chest (`Cabinet.StoreCabinetItem` by its Cabinet
  row; not yet watched in the game). Not offered for a piece you're wearing; once asked, it says
  Storing… for five seconds (a second ask would be a second store). The dresser has no such call
  the plugin knows: it says to store them there.
- **Worn in game** (public looks only): **Take a shot** hides the HUD (as Scroll Lock does) for a
  few frames, grabs the game's frame before Dalamud draws its windows, shows the HUD again, and
  makes the picture 2560 at most on its long side (1920 if no quality fits) as a JPEG under 3 MB,
  with a preview (cropped to fill 150 tall) and its size. Under it a caption, **Send it to the look**
  (outlined: the sticker's elsewhere), Take another, Discard. Sent, it goes to the edge function
  `gs-plugin-shot` and shows on the look's page under Worn in game (three of yours a look, three a
  minute, twenty a day), and the card says so with the sparkle and See it; refused, the site's words
  in amber. A shot grabbed after the HUD came back (Scroll Lock pressed meanwhile) says so under its
  preview. Watched (2560×1440, with DLSS on): the HUD hides and comes back, the picture goes up. A
  frame that never comes is given up on after five seconds and the HUD shown again.

Filling (Fill plate N, Put on plate N, Plate it) reads the dresser and armoire again the moment
before; a look sent from the site comes off the list once it's on a plate.

### Plates

**Your glamour plates 14/20** in display capitals, and at the right "As the game had them when you
last opened Glamour Plates · 2 h ago" ("Read 2 h ago", or nothing, when there's no room). Above the
plates, **What you're wearing**: whose outfit, up to six of its pieces, the look of yours it is (or
how many pieces), Try it on (as it is: nothing's changed in the game) and **Copy my game outfit** (a
new private look of yours from what you wear, named "Their name's outfit", to remix in the dressing
room; then Open it on the site; greyed when it's your look already). Then all twenty plates five
across, each a numbered card: up to six of its pieces on a raised well, the look it is (or how many
pieces) on two lines at most, "Planned: Moonlit Scholar" under it, a lilac dot by its number when a
look's planned for it on the site, the jobs of the gearsets linked to it, and **On** in ice on the
plate you're wearing; an empty one dashed, "empty" in the hand voice. Not logged in: an empty page
saying so. Not read yet: the "Not read yet" page alone, one column (no side pane): the plates, and
with them the gearsets to link, show once Glamour Plates has been opened at a dresser once (the read
is kept across sessions, in `wardrobe.json`).

The plate picked, in the side pane (352 wide): "Glamour plate", PLATE 3, how many pieces (or Empty),
its plan ("Planned on the site: the look", which opens it, and On it or Not on it yet), **Wear it**
(the sticker: `/gearset change <your gearset> N`), Try it on, **Keep it as a look** (a new private
look named "Plate 3", then Open it on the site), **Gearsets** (each linked one with its number, job
and name, Change to it (`EquipGearset`: the plate comes with it) and × to unlink; Link to a gearset,
a menu of the others with the plate each has now: `LinkGlamourPlate`) and **Its twelve slots**.

### Closet

No field and no sticker: nothing here is the one thing to do. Down the left:

- **Glamour dresser**: when it was read and Make room; the places used as a big number, "of 800
  places used · 312 free", what gathering outfit sets would free (amber), and the bar.
- The other places: the armoire, armoury chest, bags, saddlebags and retainers, each its icon, name,
  when it was read (retainers: how many read, and when the last was) or, in amber, "not read yet:
  open …", and how many pieces.
- **Your closet on Glamour Studio**, as it stands:
  - sent: "2,140 pieces, sent 4 min ago (12 new)", that it's sent by itself as it changes, **Send
    now** and **Open** (under 1000 wide, Send, and Open as an icon); "Not sent yet since the plugin
    loaded" before the first send;
  - sending: "Sending your closet…" with a bar;
  - a problem: "Closet not sent" in red, what went wrong, "Your closet is safe here; it's sent again
    in a few minutes, or now", and **Try again**;
  - sending by itself off: "Sending by itself is off (Settings): it's sent only when you press Send
    now", Send now without Open;
  - this character not asked yet: "Koko Nyan isn't sent to the site: say yes or no", and **Ask me**
    (the question again);
  - you said no: "You said no for this character: nothing of it is sent (Settings → Account to
    change it)";
  - not linked, or a token the site doesn't know: a dashed card, Link your account (Link again).

Beside it (380 wide), **your wishlist** and its count. A wish come true: the sparkle, the hand "a
wish came true!" and which pieces came off it and when (seen in your wardrobe); it stays until the
window closes (one that came true while it was shut shows when it opens). Then each piece with a
lavender dot: its name, "For the look · its dyes there" (or, once it's yours, "Yours now · in your
dresser" in ice), and × to take it off (`gs_plugin_wish_remove`: off here at once, back on should the
site say no). The list scrolls on its own; the note under it stays.

### Make room

The field: "Glamour dresser · read 2 h ago", the places used of 800 in heavy capitals, the bar,
"312 free · gathering 14 outfit sets frees up to 31 places", MAKE ROOM echoed behind; at the right
the **Make room for me** sticker and the hand ("it asks first, and does one set the first time so
you can check"), greyed with why (Close Glamour Plates first, Open your glamour dresser first, it
needs so many glamour prisms or free bag places, No set has loose pieces to gather). While it works:
"Storing sets for you · keep the dresser open", how many of how many sets, the places freed so far
and the set at work, and **Stop** ("Stops once the step the game is answering is done"); STORING
echoed (TAKING OUT for duplicates). When it's done: "Made room · 31 places freed" (or Made no room),
how many stored, and what's left said plainly in amber.

Below, two columns:

- **Keep as outfits** (how many, and "frees up to N" in amber): each outfit set (`MirageStoreSetItem`)
  with two or more loose pieces in the dresser (one, when the set's stored already), the most freed
  first; a stored set's own pieces don't count as joining it again. Each row its icon, name and line
  (so many pieces loose; "The set's in your dresser: 2 join it"; in your bags, high quality, dyed)
  and Frees N (or Tidier). During a run each row says where it is: Done in ice with what it freed,
  the one at work turning, Next, Waiting (dimmed), Not taken.
- **Not in any look or plate**: loose pieces none of your looks, the looks you love, looks sent or
  planned for this character, your plates or your outfit uses (shown only once your looks have been
  read), as many as sit beside the sets, then "and 29 more".
- **Duplicates**: what they are in a line ("4 second copies with the same dyes, and 1 set stored
  twice") and **Take out duplicates**. While a run's going, **While it works**: how it goes, in words.

**Make room for me** (the field's sticker; in the dresser's panel a button pressed twice): the
plugin gathers the sets for you (`Game/OutfitStorer.cs`). The game stores
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
(Try it on one set: the one with the fewest pieces, named in the question), so you can check it
before it does them all. Before a set's first piece leaves the dresser, every piece is found again, your bags are
checked for room and a Unique piece you hold is left; each restore is waited for until the dresser
has one copy fewer and a new bag place holds it, the store until those places are empty, and a
stored set's place is found again before pieces go into it. A piece that doesn't come still lets
the ones that did go in as the set (when enough for one), then it stops and says which pieces are
left in your bags. It says first what it'll do, as a question: the first time "Try it on one set?"
(the set, its pieces and prisms; what it frees, the dyes lost, the bag places it needs and to keep
the dresser open; Try it / Not now), after that "Store the sets for you?" (every set, the places
freed, the prisms and how many you have, the free bag places it needs at a time; Store them / Not
now). It needs the dresser open and Glamour Plates closed. A new set needs two pieces or
more, loose or in your bags (a set's pieces left in your bags by a run that stopped are put away as
the set, freeing nothing but tidier than loose); pieces join a set stored already one or more at a
time. A set's normal and high-quality copies are two sets: the high-quality one's id is the set's
plus a million, in the dresser and when storing it (the outfit window sent `1045518` for Plain
Pajamas of high-quality pieces; 0.6.6 to 0.6.11 sent the set's own id, and the game refused every
high-quality set), and pieces join only the stored copy of their quality (a normal eye mask couldn't
join high-quality Plain Pajamas). A set's pieces are all one quality, as the game's outfit window says ("Only items of the
same quality may be selected"): the quality with more loose pieces (a set left only because its
pieces are of both is counted and said). An undyed copy is used before a dyed one; a dyed one loses
its dye (storing takes it off, as the game's confirmation says; plates keep their own dyes), and
the ask says how many will. A piece two sets share goes to the first. A set the game doesn't take is left,
its pieces in your bags (said at the end), and the run goes on; three in a row and it stops. Stop
waits for the step the game is answering. It's worked out again from the dresser as it is when you
say yes. A run whose last set is stored as the dresser closes ends as done, not as "The glamour
dresser closed". Recordings write each step (`OUTFITS …`). New: not yet watched in the game.

**Duplicates** (the same card, and the dresser's panel): a second loose copy of an item with the
same dyes, and an undyed loose copy of a piece a stored set already holds (`DresserCleanup.Duplicates`;
copies dyed otherwise aren't duplicates), and a stored set's copy whose pieces another copy of the
set has (either quality: they look the same; the copy with the most pieces stays): the game lets a
set be stored again, by hand, with a piece the stored one has. **Take out** ("Take out the
duplicates?", Take them out / Not now: what comes out, the bag places it needs, never the last copy,
nothing thrown away) restores them to your bags one at a time (`Game/DresserTaker.cs`), each waited
for, never a Unique piece you hold. A loose copy is found again by item and dyes, and checked against
the dresser as it is just before it's taken (`DresserCleanup.StillDuplicate`): another loose copy
with the same dyes still there, or, undyed, a stored set still holding the piece; else it's left
(0.6.16 counted any other copy, whatever its dyes, and any stored set). A set's copy is chosen by its
pieces, not only its place (`DresserCleanup.SpareSetCopy`): the one at its place while it's still
there, else the nearest below (places close up downwards as earlier ones leave), then above, and
only a copy whose pieces the set's other copies (either quality) hold between them, never one whose
pieces no other copy holds (0.6.16 took whichever copy was at the place, which could be the one
meant to stay); it's restored whole (its pieces all come to your bags, so it needs that many free
places). It stops when your bags are full. A run whose last piece arrives as the dresser closes ends
as done. Nothing's thrown away: they're yours to sell or discard. Recordings write each step (`TAKE
OUT …`). New: not yet watched.

## Beside the game's windows

A small Glamour Studio panel (300 wide, in the window's look with 14px corners) docked to the edge
of the game window you have open, its top level with the window's, moving with it as you drag it; on
the side with room (right by default; Settings can pin a side). As tall as what's in it, whatever the
game window's height (it measures what it drew each frame), scrolling only when the screen runs out.
It's gone with its window, while the HUD's hidden, in cutscenes and in group pose.

**Its header** (54 tall with its line): the logo, GLAMOUR STUDIO in small lilac spaced capitals over
what it's beside in Bricolage capitals (At the dresser, Plate 3, Your plates, by look, the outfit
set being stored, the piece being dyed, Trying on, At the armoire, Their name's outfit, On Retainer's
name, Wishlist), then Refresh, **Open Glamour Studio** (the plugin's window on the screen that
matches: from Glamour Plates the plate's planned or picked look, else Plates; from the dresser, Make
room; from the armoire, a retainer, the market board, a shop or the dye window, Closet; from the
fitting room, the look on; else Home) and the fold, quiet and round. Folded, a slim tab stays on the
window's edge: the arrow back out, its one number in lilac mono (the looks ready at the dresser, "20+"
past twenty; the plate open; your wishlist's length at the market board; a retainer's pieces for your
looks) and the logo under them; pointed at, "Glamour Studio: what it's beside". Folding is remembered
for each window.

**Its parts** (the mock's pane parts, `UI/Companions/Companion.cs`): sections 14 apart under a
heading in Bricolage capitals with a faint mono count; small words; a piece's row (its tile with the
corner dot, its name, a muted line under it (its dyes, or the look it's for), and a tag or a small
button: Dresser or Armoire in ice; Bags,
Armoury, Saddlebag or Retainer in amber; Wishlist in lavender with ★; Don't have in pink); a look's
row (two of its pieces, its name in Bricolage capitals, a line
with its dyes' swatches before it, a small button; lilac when it's the one picked); "and N more" to
show a list whole; a dashed card with Link your account (Settings → Account) where linking's needed;
an amber card for something to see to first; Add … to your wishlist (a small button with ★). One
panel at a time: the game window that has the game's focus, else the likeliest (Glamour Plates,
an outfit set, the dye window, the fitting room, the armoire, the dresser, Examine, a retainer, the
market board, a shop, in that order). It never opens the main window by itself; with the Glamour
Plates panel on, opening Glamour Plates doesn't open it either (Settings → Open with the plate editor
is off meanwhile). `UI/Companions/Companion.cs` is the panel, `Panes.cs` what each shows,
`Game/GameWindows.cs` the game windows' places and the few reads and presses only the panels need.

| Beside | It shows |
| --- | --- |
| **Glamour Dresser** | The dresser in a line (places used of 800 in mono, free, **Make room** to open the screen), the bar, how many pieces could leave without breaking a look. Then **Try it on one set** (once it's worked, **Store 14 sets for me**) and **Take out 3 duplicates**, each pressed twice: the first press turns it amber for five seconds ("Press again to store them") and says under it what the second will do; until then a line under each says what it could do (in amber, why it can't). While one works: what it's at, Stop, the bar in periwinkle, places freed; at its end what it made (ice) or what's left (amber). **Ready for a plate**: the looks the dresser and armoire have everything for (yours, the ones you love, and the ones sent to you; four, "and N more"), each with **Plate it**: a menu ("Put “the look” on") of the twenty plates, each its number in mono, "Plate N" and what it has on (empty, this look, a look's name or how many pieces) with "planned: the look" under it: the plate planned for this look first (lit), then the empty ones, then the rest; seven show and it scrolls. The one you pick is filled in one go (`UI/Views/PlateIt.cs`): it presses the dresser's own Glamour Plates button (`[u:5]`, as the first recording saw), picks the plate as clicking it does (`[i:17, u:plate−1]`, plate 1 to 14 and 1 to 3 in a recording), waits for the game to show it picked and 0.3 s more for its slots, and fills it as Fill plate does. Nothing's saved: you press Save in the game's window. With the plate open in Glamour Plates holding changes not yet saved, the menu still offers every plate, but picking another is refused with a toast ("Plate 3 has changes not saved: save them, or put it back, first"); nor does it start while Make room for me works. A look's own page has the same, **Put on a plate…**, while the dresser's open. **Bring to the dresser**: your looks' pieces still in your bags, armoury chest, saddlebags or on retainers (six, "and N more"). |
| **Glamour Plates** (at a dresser) | Follows the plate picked in the game. With changes you made by hand and haven't saved, an amber card, "Plate 3 has changes not saved: save them, or put it back, first", and Fill waits (it would mix its pieces in with yours). Only changes by hand: right after the plugin fills that plate (from the window, Plate it or the panel, with any look) the card isn't shown; changing the plate's pieces by hand afterwards lets the fill go and brings the card back. Else the plan: "Planned on the site" (or "Picked here", a look picked in the panel: **Another look** lists the ready ones, then the rest of yours; a pick made here is for its plate, Back to the plan undoes it), its name (opening it in the plugin), On it or Not on it yet, its pieces. **Slot by slot** (so many to change, or all on it): each slot's piece with a tag: On it, Changes, Armoire, In your bags… (and the other places), Don't have, Emptied or Kept. The dyes: from your bags (ice), each to dye in the editor (amber), and the missing bottles to put on your wishlist. **Fill plate 3**, the sticker across the panel ("It's on the plate already" when it is). Filled, not saved: an ice-edged card, "Filled · not saved yet", the hand "now press Save in the game", Fill it again and Another look (the look picked there can be filled over the fill at once). Saved: "Saved · on plate 3" and how to wear it (the game won't change gearset at a dresser: close Glamour Plates and the dresser, then Wear plate 3). No plan: the ready looks, each with Fill with it, or plan one on the site. |
| **Glamour Plates** (from the Character window) | Every plate with something on it, by its look's name (or how many pieces), its linked gearsets' jobs, **On** for the one you wear, **Wear** for the rest (it picks the plate, `[i:17, u:plate−1]`, and once the game shows it picked presses the window's own apply, `[i:15, u:0]`, which applies the plate picked: 0.6.6 sent the plate's number with the apply and the window applied plate 1; nothing's applied if the plate isn't shown picked within two seconds. `/gearset change` is refused while this window's open, "…while occupied", a recording saw; greyed in combat); the plate picked in the game highlighted; "and N empty plates". It fills no plate: that's the dresser's Glamour Plates. |
| **Armoire** | Your looks' pieces stored in it (plates take them from there); what in your bags and armoury chest it takes, each with **Store**, and **Store all** (one by one, half a second apart, a refused piece asked again, then said); how many pieces in your dresser it would take (and which), each a place freed once moved. |
| **Storing an outfit set** | The set's name in the header; dresser places freed by storing it (its loose pieces, less the one place the set takes); your looks that use it and which of its pieces; each piece and where it is. |
| **Fitting Room** | Your looks (**Ready** or **All**, a small segmented pick) to step through with the arrows ("3 of 12"), each put on in the fitting room (the panel stays while it reopens): its name, where each piece is, the ones you don't have onto your wishlist, and **Wear it · plate N** when the look's on a plate. **Tried on by hand**: what the fitting room has on that isn't the look's (with no look, everything on; with one, the extra pieces too), and **Keep it as a look** (everything it has on, as a private look "Tried on"). |
| **Examine** | How many of their pieces you have (a ring), each piece and where yours is, the missing ones onto your wishlist, Try it on, Keep it as a look (named after them, "Their name's outfit", as Home's; "An outfit I saw" when the name can't be read). "reading their outfit…" until two reads agree. |
| **Dye** | The piece being dyed (the first dyeable gear in the window's values, else the one piece its preview shows: not yet watched), your looks that have it and the dyes each wants, the bottles in your bags for them, the missing ones onto your wishlist. |
| **A retainer's bags** | Your looks' pieces on this retainer that aren't in your dresser or armoire; how much glamour on it no look uses, and what of it the armoire takes (and which); your other retainers, each with how many of your looks' pieces it has (or not read yet). |
| **Market board** | Your wishlist: what the market board sells (each with the look it's for, the cheapest listing while its results are open, **Search**, which types it into the market board's search and searches: not yet watched, else it copies the name), and what it doesn't (untradable), each with **Take off** (off your wishlist). |
| **Shops** (`Shop`, `ShopExchangeItem`, `ShopExchangeCurrency`, `ShopExchangeCoin`, `InclusionShop`, `GrandCompanyExchange`, `FreeShop`, `SkyIslandExchange`) | What on your wishlist the shop sells, as far as its window's values name the items (best effort: a price or an icon that happens to equal an item's number would count too). |

**Under item tooltips:** while the game's item tooltip (`ItemDetail`) shows a piece of gear, a small
box in the plugin's look (`UI/Companions/ItemExtras.cs`: the card, the strong hairline, 12px
corners), as wide as the game's tooltip (220 at least), 4px under it, or above it when there's no
room below, and kept on the screen whatever the game's tooltip does (a tall one at the top, a narrow
one at the edge). The logo and GLAMOUR STUDIO in small lilac capitals, then each line with its icon,
wrapped: which of your looks have it (a hanger), the plates it's on, on your wishlist (lavender ★;
"and yours now" once it is), whether your dresser has a copy or it's in your armoire (ice ✓).
Nothing when none of that's so.

**Right-click menu:** on gear in your bags, the armoury chest, a retainer's, an item linked in chat
(and the market board, recipes, shops), a **Glamour Studio** entry, its name in the plugin's lilac
in the game's own menu (no prefix before it): Add it to a look… (your looks;
the piece goes in its slot, a ring on the free hand, `gs_plugin_look_put`), Try it on with a look of yours that has it (the
whole look in the fitting room; a submenu of them, up to twelve, when more than one has it), Which looks use it (a line in chat), Put it on your wishlist (when it isn't
yours), Take it off your wishlist (when it's on it), Store it in the armoire (from your bags, while the armoire's open).

Recordings write down which panel shows, where, folded or not, a look picked for a plate, and the
piece the dye window has (`PLUGIN … panel …`); and the market board's and shops' windows opening and closing, never their words (sellers' and retainers' names), as Examine's.

## Settings

Plain and quiet, as the site's account pages: one column (640 at most) in the middle, **General**
and **Account** as a pick-one pill, each group a muted label over a card of rows, each row a
setting's name, one line of why (the longer words its tooltip) and its switch or button at the end. A
row that can't be changed now is dimmed, with the reason as its line. The version and What's new
under the column.

**General**: Glamour plates; Beside the game's windows; In menus and tooltips; Your closet on the
site; What the plugin has read; Making a report (below).

| Setting | Default | What it does |
| --- | --- | --- |
| Empty the slots a look leaves bare | on | A look with no earrings empties the plate's earrings, so the plate is the look exactly. Weapons are left alone unless the look has one (a plate's weapon is its job's). |
| Dye pieces with the dyes in your bags | off | A piece whose copy is dyed otherwise than the look gets the look's dyes from bottles in your bags (the game spends them when the plate is saved). New (an amber tag), and not yet watched in the game: off, the plugin says which pieces to dye in the plate editor. |
| Open with the plate editor | on | Opening Glamour Plates opens the window, on Home (off, and dimmed, while the Glamour Plates panel is on). |
| Show Glamour Studio beside the game's windows | on | The panels beside the game's windows (above). |
| Side | Auto | Which side of the game window a panel takes: Auto (the side with room), Left or Right. |
| Start folded | off | A window's panel you haven't opened or folded yourself starts as its tab. |
| Each window's panel | on | **Choose** unrolls a switch for each: Glamour Dresser, Glamour Plates, Armoire, Storing outfit sets, Fitting Room, Examine, Dye, Plates from the Character window, Retainers' bags, Market board and shops. |
| Glamour lines under item tooltips | on | The box under a piece's tooltip. |
| Glamour Studio in the right-click menu | on | The right-click entry on gear. |
| Keep it up to date by itself | on | Sends your closet as it changes (see [The site](#the-site)). |
| Send my plates and outfit | on | Your plates and what you wear go along, with your character's name and world (only you see them), for the site's Plates and Copy my game outfit. |
| Say in chat when a wish comes true | on | A line in chat when pieces on your wishlist are seen in your wardrobe (they come off the list either way). |
| Forget this character | | Forgets what the plugin has read of the character you're playing (it's read again as you play); it asks first. |

**Account:** **Glamour Studio account**: linked, "Linked to Glamour Studio" with the token masked and
Unlink (it asks; a danger question); a token the site doesn't know, Link again; not linked, "Not
linked", Get a token (the site's Settings → Glamour Studio → In-game plugin), the token's field and
**Link** (the only sticker in Settings; a token that can't be one says why under it: it starts
`gsp_` and is 47 letters long). Under it, to keep the token to yourself, Get a token and Open Glamour
Studio. Then **Characters**: the one you're on first ("you're on it"), then each you've answered for,
by name, each its face (on the periwinkle when it's sent), its world, where it stands (Sent to the
site, Not sent, Not asked yet in amber, Yes · telling the site…, No · telling the site…, Checking with
the site…, Link your account to send it) and its switch (Send to the site); the card at half strength
while not linked. What's shown of each on the site is the site's Settings → Glamour Studio →
Characters.

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

In Settings it's the last group of General, its card red-edged while it records. Off: **Record
what I do** and Start (with the red recording dot). Recording: "Recording · 1,204 lines · 4:12" (the
time from the recorder's own start, `Recorder.StartedAt`, so a recording begun by `/gstudio record`
while Settings was shut is timed right too) with Stop, a field to **Mark** a step (Enter marks too;
an empty mark isn't written), and **Write down the windows' values**. Stopped: "Stopped · 1,204 lines
· Mark 6 written", Start again (this one goes, so copy or save it first), then **Copy the
recording** and **Save it as a file** side by side, and once saved where it went (from
`pluginConfigs` on) with Show, which opens its folder. Copy and Save are offered only once it's
stopped (`/gstudio save` saves at any time).

**Write down the windows' values** (`/gstudio dump`) adds every value the open glamour windows
hold and the plate editor, dresser, plates and armoire in full, a hundred values to a line. The
same line again is counted ("the line above 12 times in all"), not written again. **Copy the
recording** puts it on the clipboard with what the plugin knows (the plugin's, Dalamud's and the
game's versions, the plugins loaded, the recorder's hooks, character, settings, what's been read,
the last send); **Save it as a file** (`/gstudio save`) writes it to
`pluginConfigs/GlamourStudio/recordings/gstudio-<time>.txt`. The recording's header (when it started,
what's been read and when) and that "what the plugin knows" give their times in the PC's local time
(they're kept in UTC). It keeps the last 20,000 lines and stops
by itself after two hours. It sends nothing anywhere, and leaves out other people's names (the
Examine window's words and values, the names in a menu on someone). `/gstudio mark` and
`/gstudio dump` start a recording when none is going, but not over a stopped one that hasn't been
saved or copied since its last line: a line added after a save or a copy means it isn't handed over
any more, so a new mark or dump asks you to save it first ("the last recording isn't saved yet").

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
| How you look | your character's 26 customize bytes (`IObjectTable.LocalPlayer.Customize`), and the colours they pick from the game's `chara/xls/charamake/human.cmp` (`Core/Appearance.cs`) | with what you wear (since 0.6.14) |

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
| `gs_plugin_sync` | your wardrobe changed and settled for 10 s, at most every 3 min (and only when what's sent changed); Send it now; saying yes; linking. **Only for a character the site takes (`yes`)** | the character you're on (key, name, world), its pieces each where they are, the places read in full, its plates and what it wears, with how it looks (`p_outfit.appearance`: its customize bytes and their colours as `#rrggbb`, for the site's dressing room to draw it as its mannequin) |
| `gs_plugin_looks` | the window opens, every 10 min while it's open, Refresh | your looks (300), the public ones you love (200), your wishlist's items |
| `gs_plugin_inbox_list` | as the window opens (any way: the plugin list, `/gstudio`, a panel's arrow), every minute while it's open, every 15 otherwise | the looks sent to the game, each whole, with the plate meant |
| `gs_plugin_inbox_remove` | a sent look went on a plate | it's off the list |
| `gs_plugin_save_look` | Keep it as a look (a plate, someone examined, what the fitting room has on), Copy my game outfit | a new private look of yours |
| `gs_plugin_plans` | the window opens and with the looks, and when you change character (30 s at most); only for a character the site takes | that character's plate plans, each look whole |
| `gs_plugin_wearable` | the window opens, every 10 min while it's open, after a sync that changed the closet, on another character | public looks you own every piece of (Wear today), the most loved first: as the character you're on (`p_character`) when the site takes it, else with all your characters' pieces |
| `gs_plugin_wish_add` | a panel's "Add … to your wishlist", a look's plate slip's, the right-click menu's | up to 40 pieces (item and dyes) onto your wishlist; a piece on it already stays |
| `gs_plugin_look_put` | the right-click menu's Add it to a look | the piece into one slot of one of your own looks |
| `gs-plugin-shot` (edge function) | Send it to the look | multipart: token, look, caption, the JPEG; the site checks the token, the look (public), the limits, and stores the shot |
| `gs_plugin_shot_character` | right after a shot's sent, for a character the site takes | which character wore it (the site shows it on the shot only while you show that character on your shots) |

`gs_plugin_sync`'s answer has `wishes_got`: the items it took off your wishlist because this
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
of its own at the top of the screen, 40px down (440 wide; ink with 28px corners and the strong
hairline, no glow): the logo and the eyebrow "A new character on Lich", "Send Koko Nyan's wardrobe
to Glamour Studio?" in display capitals, what goes, and that it stays private and nothing is sent
until you say yes; whether the window's open or not; gone in cutscenes and group pose and while the
HUD's hidden, as the panels are. **Yes, send it** (the sticker): told to the site, then the closet
goes at once, its plans and Wear today read as that character; **No** (outlined): told to the site,
which lets go of what it had of it; **Not now** (quiet): not asked again about it until the plugin
loads again (Closet's **Ask me** asks again). A line in chat says what you chose. Not linked,
nothing's asked (nothing could be sent); on linking it is. The bar's pill says "This character isn't
sent yet: say yes or no" or "This character isn't sent to the site"; while a yes is on its way, "Yes ·
telling the site…"; and when the site couldn't be reached for its answer, "Glamour Studio couldn't
ask about this character" (it's asked again a minute later).

**An answer you give stands until the site has it** (0.6.15). Answering (the question, or Settings →
Account) puts the character in `Pending` (kept in the settings, so closing the game meanwhile changes
nothing): the site's old answer is set aside, so nothing of that character is sent while the new one
is on its way, and it's told again until the site confirms it (a failed telling is retried; Settings
shows "telling the site"). A reading of the characters that started before your answer, or comes
while it's pending, never overrides it; answering twice quickly tells the site the latest. Should the
site answer no to a yes, no is taken (the safe way round).

A sync the site refuses for a character you said no to there (hint `refused`) isn't shown as a
problem: the character's answer here becomes no. Older plugins (0.4.x and before) never ask: the site
keeps what they send of a character no one's answered for (private), and refuses one that's no, with
words saying to update the plugin.

## The code

| Folder | What |
| --- | --- |
| `Plugin.cs`, `Services.cs`, `Configuration.cs` | Loading, the command, the framework tick (reading, the site, opening with the plate editor), settings. |
| `Core/` | Game-free, tested: slots and the game's orders (`Slots`), looks and their JSON (`Looks`), the wardrobe and what's sent (`Wardrobe`), asking about each character (`Consent`), the plan for a plate (`PlatePlan`), how ready a look is (`Readiness`), the dresser's places, sets to gather and duplicates (`Cleanup`: `DresserCleanup`), your character's colours (`Appearance`), the site's calls (`SiteLink`), words and numbers (`Words`, the invariant culture). |
| `Game/` | The game: its sheets (`GameData`: items, dyes, armoire rows, outfit sets, jobs), reading your wardrobe (`WardrobeReader`), the plate editor and wearing a plate (`PlateEditor`), try-on (`FittingRoom`), the armoire (`Armoire`), gearsets (`Gearsets`), the Examine window (`Examine`), gathering sets (`OutfitStorer`) and taking out duplicates (`DresserTaker`), the game windows the panels sit by (`GameWindows`), the recorder (`Recorder`, `AtkText`). |
| `Session/` | `SiteSync`: the site in the background. `Shots`: taking, making and sending a shot. |
| `UI/MainWindow.cs` | The window: the bar (the brand, the five places, your character's pill, Refresh, Settings; back and the title on a screen below), the screens, the question over the window, toasts, a failed screen's note. |
| `UI/Views/` | The screens, each a part of `MainWindow`: `Home`, `LooksView`, `LookView` (a look, the plate slip, Put away first, Worn in game), `PlateView`, `ClosetView`, `CleanupView` (Make room) and `AutoRoom` (Make room for me and taking out duplicates: the questions, the runs), `PlateIt` (Plate it), `Settings`, `Characters` (the question about a character, and Settings' characters), `Shared` (where a piece is, dyes' words). |
| `UI/Companions/` | The panels beside the game's windows (`Companion`: choosing the window, docking, folding, the header and tab, the panels' shared parts; `Panes`: what each shows; `ItemExtras`: the tooltip box and the right-click entry). `Game/GameWindows` holds their game side. |
| `UI/Kit/` | The design system, drawn with ImGui's draw lists: `Palette` (the colours), `Fonts` (the faces and sizes, the Noto fallback and its rebuild), `Txt` (text as the browser lays it: line boxes, spacing, wrapping, runs, the fallback), `Textures` (the logo, holo, the sparkle, the cloud), `Theme` (ImGui's own style), `Kit` (buttons, switches, fields, tooltips, menus), `Sticker`, `Field` (the field, `Tint`, the echo, callouts, the slip, display and hand words, holo, the logo), `Parts` (tags, item tiles, the palette strip, the seal, the sign and empty pages, rings, the bar, headings, tabs, combos, faces, dots), `Bar` (the window's bar: the brand, a place, the pill), `Layout` (panes, columns, chips), `Rows` (wrapping rows, pick-one tabs), `Frame` (toasts, unwinding a frame that threw), `Icons` (Lucide's paths). |
| `UI/Model/` | The Kit's game-free parts, tested: SVG paths (`SvgPath`), dashed edges (`Dashes`), which characters a face has and the fallback's ranges (`Glyphs`), the echo's words, initials, turning the seal and sign, the bar's fit (`Sheet`), a look's sheet (`LookSheet`), a recording's clock (`Elapsed`). |
| `Resources/` | `Fonts/` (Bricolage Grotesque, Shantell Sans, Figtree, Geist Mono, each with its licence) and `Textures/` (the logo at four sizes, `holo.png`, `spark.png`, `cloud.png`), embedded in the DLL. |
| `images/icon.png` | The icon Dalamud's installer shows (512 × 512). |
| `tools/make-icon.mjs` | Renders `tools/icon.svg` (the site's mark: kokos-tools `src/features/marks.ts`) with Playwright's Chromium to `images/icon.png` and, without its light, to the logo textures `Resources/Textures/logo-32/64/128/256.png`: `node tools/make-icon.mjs` when the site's mark changes. |
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

### 0.7.0

The plugin rebuilt in the site's new look, **Character Sheet**, to the mock-up in `docs/redesign`
(see [The window](#the-window)), and the fixes its reviews found.

- **The window**: the rail down the left is gone; the site's bar runs across the top (Home · Looks ·
  Plates · Closet · Make room with their counts and the holo underline, your character's pill with
  what's going on, Refresh and Settings), and the screens have the whole width. Each screen has one
  field (the periwinkle moment: the plate open at a dresser, a look's sheet, the dresser's number,
  the first run) and one sticker (its one thing to do).
- **The look**: the ink and the field instead of the violet night; ice for what you have, pink for
  what's missing, lavender for your wishlist, amber for what to see to, red only for what went wrong
  (mint and orchid are gone); holo for what's special. A look is a character sheet: its pieces as
  callouts round its middle, its name echoed behind, its dyes as a palette strip, the maker's seal;
  the plate card is a plate slip.
- **The type**: Bricolage Grotesque in capitals for titles and names, Shantell Sans for the hand
  voice, Figtree to read, Geist Mono for numbers (Outfit's gone). Names in any script draw: a
  character the faces haven't got is drawn in Noto Sans CJK, added to the atlas when it's first
  shown (no more "?").
- **The site's words**: Wear today (was Wear now); Copy my game outfit for what you're wearing.
- **Home**: the looks ready for a plate as six small character sheets; Wear today as rows beside.
- **Looks**: the search has a × to empty it, and Escape empties it too.
- **Plates**: "Your glamour plates 14/20" with when the game last had them beside it; the plate
  you're wearing tagged On; "Planned: the look" on each card; Wear it is the plate's sticker.
- **Closet**: the site card says how your closet stands (sent, sending, a problem with Try again,
  sending by itself off, not asked yet with Ask me, said no, not linked); the wishlist shows the
  look's dyes for each piece; a wish come true stays until the window closes.
- **The panels**: the dresser's Store sets and Take out duplicates are pressed twice, the first press
  amber and saying what the second does; beside Glamour Plates, Fill waits while the plate has
  changes of yours not saved; the fitting room's Keep it as a look is offered when pieces were tried
  on by hand over a look too.
- **Tooltips**: the box under an item's tooltip has icons and the honest colours, sits above the
  game's tooltip when there's no room below, and stays on the screen. The right-click entry is
  "Glamour Studio" in lilac, with no prefix.
- **The pill** says "Yes · telling the site…" while your answer about a character is on its way,
  "Glamour Studio couldn't ask about this character" when the site can't be reached for it, and
  "Closet not sent: see Closet" when a send failed.
- **Take out duplicates**: a set's copy to take out is chosen by its pieces, never one whose pieces
  no other copy holds (0.6.16 took whichever copy was at the place read, which could be the one meant
  to stay); a loose copy is checked against the dresser as it is just before it's taken (0.6.16
  counted any other copy, whatever its dyes, and any stored set).
- **Make room for me and Take out**: a run whose last set or piece arrives as the dresser closes ends
  as done, not as "The glamour dresser closed".
- **Recordings**: a line added after a save or a copy means it isn't saved any more, so a new
  `/gstudio mark` or `dump` asks first (a save in the middle of one, then more lines, then a mark,
  started over them); Settings offers Copy and Save once it's stopped, and shows how long it's been
  going, from the recorder's own start; the times in its header and in what the plugin knows are
  local (they were UTC, unmarked).
- Numbers are written the same whatever Windows' language is (the invariant culture).
- `tools/make-icon.mjs` writes the logo textures the window draws as well as `images/icon.png`.

### 0.6.16

The site's "Moonlit Atelier" redesign, in the plugin, and your character's colours read right.

- **Your character's lipstick and facial-feature colour** were read from the wrong places in the
  game's colour file (`Core/Appearance.cs`, sent with what you wear): a light lipstick from colour
  1792, one of the file's unused palettes (yellow lips on the site), and the features' colour from
  the eyes' palette. Now the light lips are at 1024 and the features at 768, as Penumbra.GameData's
  `CmpData` lays the file out (Glamourer reads it so) and the site's extraction reads it. (The site
  reads both from your character's bytes too, so a character sent by 0.6.15 shows right there once
  it's put on again.)
- **The new look**: the window a violet night, lilac for words, rings and where you are, the one
  thing to do a violet pill that glows when pointed at, orchid for the pieces you don't have yet,
  mint for what's yours, the progress bar iridescent; the segmented switches' pick glides to it.
- **The new type**: Outfit for titles and headings, in normal capitals (Syne capitals before),
  Figtree to read and for labels and eyebrows, Geist Mono for numbers only (DM Sans, and DM Mono
  in spaced capitals for labels, before).
- **The mark** at the header's left, and the plugin's icon: since 8 November 2026 the site's new
  mark (kokos-tools: `src/features/marks.ts`), a white hanger in an arched violet mirror
  (`MainWindow.Mark`, drawn flat in the mark's 30 × 30 grid; the icon's mirror is the studio's
  blend of violet `#7b55f0` into orchid `#c03a9c`; a crescent moon over the hanger came off the
  same day); a white hanger on a
  violet rounded square before (rose before that).
- **Wishlist**: the shopping list is called your wishlist everywhere, as on the site.

### 0.6.15

A deep review of the whole plugin, by area, with every finding checked against the code, then the
fixes reviewed again for regressions.

- **Your answer about a character stands until the site has it**: a "No" could be undone by the
  site's older "yes" (a failed telling was never retried, and a reading in flight could flip it back),
  and the character kept being sent. Now nothing of it is sent until the site confirms your answer.
- **Make room for me**: the run's real ending is no longer covered by "Storing N sets…" when it ends
  at once; Stop always stores what came of a set first; a run can't hang when the dresser's data goes
  away mid-step (a zone change); the first try is the smallest set, named the same in the question,
  and its prisms and bag places are checked for that set alone; places freed no longer count pieces
  that were already in your bags; Make room and Plate it can't run at once.
- **Take out duplicates**: no longer re-checks one skipped piece every frame after a refusal; it
  stops in combat; the second copy of a set is the one taken out when places close up.
- **High-quality and outfit sets**: the outfit set window's set is the one holding every listed piece.
- **Plates**: filling stops if the dresser changed since it was read (a slot would take another
  piece); a fill no longer shows as on the plate once the plate was thrown away or changed by hand;
  the Home hero's Wear button (always greyed out at the dresser) is a sentence saying what to do.
- **Panels**: a look picked in a panel that's also the plate's plan reads as the plan; a plate that
  keeps slots a look leaves bare counts as on it when "Empty the slots" is off; the armoire's Store
  all keeps its spacing and tries a refused piece again, then says which didn't go; the fitting room
  names the fullest matching look; "20+" ready when there are more; a pane that fails is logged once.
- **The window**: an open look follows the site's reads (updated, or closed if it's gone); unlinking
  leaves it; a screen that fails says so instead of going blank; an open question goes when the
  window closes or the character changes.
- **Reading**: the dresser is read only from the dresser (not from Glamour Plates opened at the
  Character window, which could show another character's); a wardrobe.json that can't be read is set
  aside, not overwritten; your character's colours are worked out once a session again.
- **Shots**: never stuck "sending" or "taking" (timeouts), no texture released twice or leaked.
- **The site**: gentler retries (a counted call no longer every minute after a failure); answers for
  a character you've left are dropped; a failed "done with" is tried again; calls are built off the
  game's thread.
- **Speed**: text measuring, fitting, spaced capitals, paragraphs and dashed edges are cached; Sans 10
  and Syne 13 are loaded (they drew at 11 and 15); missing game icons aren't asked for every frame;
  the panels' armoire, fitting room and retainer lists, plans and plate menu are cached; long lists
  skip rows out of sight; a recording's size is capped.

### 0.6.14

- **Your character as the dressing room's mannequin.** With what you're wearing, the plugin now
  sends how your character looks: its race, clan and sex, height, face, hairstyle, facial
  features, bust and tail (the game's customize bytes as they are), and its skin, hair,
  highlight, eye, lip and feature colours read from the game's character creator palettes
  (`Core/Appearance.cs`; `human.cmp`: the shared palettes first, eyes at 0, highlights at 256,
  dark lips at 512 and light at 1792, then each clan and sex's skin and hair). The site's dressing
  room offers it in its Character menu as **Your characters**. Only for a character the site takes.

### 0.6.13

- The right-click menu's **Try it on with …** is back (0.6.10 took it out by mistake: the "Try On"
  that did nothing was Glamourer's, the purple G). With more than one of your looks using the
  piece, it's a submenu of them.

### 0.6.12

From a run storing high-quality sets:

- High-quality sets are stored: the game names a set of high-quality pieces by the set's id plus a
  million (as it does a high-quality item), and refused every one sent with the set's own id
  (Bouncer's Attire, Dalmascan Attire, Plain Pajamas).
- A set's normal and high-quality copies are two sets: pieces join the stored copy of their
  quality, or make the other quality's set anew.
- Take out duplicates takes out a stored set's copy that another copy of the set has every piece
  of (the game lets you store a set again with one piece: Noir Attire, Quaintrelle's Attire,
  Southern Seas Shirt & Trousers); restored whole, its pieces to your bags.

### 0.6.11

- Take pieces off your shopping list in the plugin: × on Closet's list, Take it off the shopping
  list in the right-click menu, Take off on the market board panel's untradable pieces (the site's
  new `gs_plugin_wish_remove`).
- **Refresh**: the round arrow at the top of the window and in each panel's header, and
  `/gstudio refresh`, read your looks, list and plans from the site and your wardrobe from the
  game again. The site's looks are kept fresh while a panel's shown too, not only with the window
  open.
- Closet's On the site card: Send now and Open were cut off (drawn narrower than their words).

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
