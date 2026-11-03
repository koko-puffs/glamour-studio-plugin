# Recordings to make with the recorder

These recordings show exactly what the game does when you do each thing by hand, and what it does
when the plugin does it. With them, the plugin's guesses about the game (marked "not yet watched in
the game" in DETAILS.md) can be checked and fixed. Each step has an id (A1, A2, …): put it at the
start of its mark, so each part of a recording can be matched to its step.

## Before you start

1. Install Glamour Studio **0.3.0** or later. Settings' footer says the version.
2. Make sure nothing else changes glamour while you record: turn off Glamourer, Glamaholic, Penumbra
   redraws and similar plugins for these sessions, or say which ones were on.
3. Log in to the character you'll use, and stand where the section says.
4. Recordings need nothing on the site, except section I.

## How to use the recorder

**The controls.** Settings (the sliders at the top right) → General → **Making a report**:

| Row | What it does |
| --- | --- |
| Record what I do: **Start** / **Stop** | Starts or stops a recording. While on, the header's pill says "Recording what you do · N lines". |
| Mark a step: field + **Mark** (or Enter) | Writes your words as a MARK, with the whole state as it is then. Only shown while recording. |
| Write down the windows' values: **Write down** | Writes every value the open glamour windows hold, and the whole state. Only while recording. |
| Copy the recording: **Copy** | Puts the recording, with the versions and your settings, on the clipboard. |
| Save it as a file: **Save** | Writes it to a text file and opens its folder. |

**The same from chat** (it works when the plugin's window is hidden: with the HUD hidden, in Group
Pose, in a cutscene):

- `/gstudio record`: start, or stop.
- `/gstudio mark <id and what you'll do>`: a mark (starts a recording if none is going).
- `/gstudio dump`: write down the windows' values (starts a recording if none is going).
- `/gstudio save`: save it as a file. Chat says where.

Saved files are in `%AppData%\XIVLauncher\pluginConfigs\GlamourStudio\recordings\`, named
`gstudio-<date>-<time>.txt`.

**For every section:**

1. Get the section's "you need" ready **before** starting.
2. Start a new recording: `/gstudio record`. Chat says it's recording.
3. For **each step**:
   1. Mark it **before** doing it, with its id first: `/gstudio mark A3 Body from the dresser`.
   2. Wait a second.
   3. Do the step **by hand, slowly**, one click at a time: no shortcuts, no dragging when the step
      says click.
   4. Wait **two seconds** after the last click: the game answers the server a moment later.
   5. Where the step says **DUMP**, run `/gstudio dump` with the window it names still open.
4. If anything unexpected happens (an error, a message, the wrong piece, nothing at all), mark it
   at once: `/gstudio mark A3 NOTE the slot stayed empty`. Notes are worth more than anything.
5. When the section's done, `/gstudio record` to stop, then `/gstudio save`.
6. Rename the file after its section (`gstudio-A-plates.txt`, …) and send it.

**Good to know:**

- One recording per section. A recording keeps the last 20,000 lines and stops by itself after two
  hours; one section fits easily.
- `/gstudio mark` and `/gstudio dump` won't start over a stopped recording that hasn't been saved or
  copied: save it first (it says so in chat).
- A recording has your character's name and world in its header. Other people's names (Examine,
  menus on people) are left out.
- Don't copy-paste a recording into a chat with a length limit: send the file.
- If the game shows a message or error, don't close it before your note: the recorder writes it
  down as it comes.

## A · Glamour plates by hand, and filled by the plugin

**Done (three recordings):** A1 to A12, A14 to A17. **Still to do:** A13 again with 0.4.1 (in
0.3.3 every dye went to the weapon: fixed). Same as before: Settings → Dye pieces with the dyes in
your bags on, a look with a dyed piece whose dresser copy has other dyes, Put on plate 3 (or Fill
plate 3 in the panel), DUMP, then Save and DUMP again; note the bottles before and after.

## B · The glamour dresser

**Done (0.3.3):** B1 to B9.

**You need:** a glamour dresser; 2 gear pieces in your bags that the dresser takes; a full outfit
set's pieces in your dresser or bags if you have one (any set the dresser can keep as one outfit),
and if you can, a set with a piece missing.

| Id | Do this | DUMP |
| --- | --- | --- |
| B1 | Open the glamour dresser. | yes |
| B2 | Click through its tabs (each slot type), then to page 2 and back. | |
| B3 | Store one piece from your bags into the dresser, the way you normally do. | yes |
| B4 | Take one piece out of the dresser back to your bags. | |
| B5 | Right-click a piece in the dresser and look at the menu (then close it). | |
| B6 | Type something in the dresser's search, then clear it. | |
| B7 | Open **Outfit Glamour** (storing a set as one outfit) and pick a set whose pieces you have. | yes, with its window open |
| B8 | Store that set as one outfit. | yes |
| B9 | If you have a set with a piece missing: pick it and try to store it. Note what the game says. | yes |
| B10 | Close the dresser. | |

## C · The armoire

**You need:** an inn room; in your bags two pieces the armoire takes (old event or seasonal gear),
one in your armoury chest, and one you're **wearing** that it takes (if you can).

| Id | Do this | DUMP |
| --- | --- | --- |
| C1 | Open the armoire. | yes |
| C2 | Click through 3 of its categories. | |
| C3 | Store one piece from your bags by hand, the game's way. | yes |
| C4 | Take a piece out of the armoire. | |
| C5 | With the armoire still open, in the plugin open a look that has a piece in your bags the armoire takes (it shows under **Put away first**) and press **Into the armoire** once. | yes |
| C6 | Press it again within 5 seconds on another piece (or the same if it's still there). | |
| C7 | Do the same for a piece in your **armoury chest**. | |
| C8 | Find a look with a piece you're **wearing** that the armoire takes: note whether the button shows (it shouldn't: "take it off for the armoire"). | |
| C9 | Close the armoire. | |

## D · Gearsets and plates

**Done (0.3.3):** D1 to D9. With 0.4.1, D6 and D7 are greyed out where the game says no: no need to redo them.

**You need:** a city (a sanctuary) and at least two gearsets; plate 3 saved with something on it.

| Id | Do this | DUMP |
| --- | --- | --- |
| D1 | Open the game's **Gear Set** list. | yes |
| D2 | Link plate 3 to a gearset **by hand** in the game's window (its menu → link glamour plate). | |
| D3 | Unlink it by hand. | |
| D4 | In the plugin's Plates, pick plate 3 → **Link to a gearset** → pick a gearset. Check the game's Gear Set list shows the link. | yes |
| D5 | In the plugin, press **Change to it** on that gearset (you're in the city). | |
| D6 | Change to another gearset by hand, then use **Change to it** again from outside the city. Note what happens. | |
| D7 | If you can, start a fight with a striking dummy and press **Change to it** while in combat. | |
| D8 | Press **×** (unlink) in the plugin. | |
| D9 | Link it again with the plugin, then log out to the title screen and back in. Mark after logging in: `D9 after login`. | yes, after login |

## E · Examine

**Done (0.5.1):** E1 to E6.

**You need:** a busy city; people nearby with glamours (some dyed); a friend or party member who is
far away (another zone, or out of range) if you can.

| Id | Do this | DUMP |
| --- | --- | --- |
| E1 | Examine someone near you. | yes, with Examine open |
| E2 | While it's open, examine someone else right away. | yes |
| E3 | Examine yourself. | |
| E4 | Examine someone far away (party list, friend list, or a search result). | yes |
| E5 | With someone dyed examined: in the plugin (Home → Examining) press **Try it on**, then **Keep it as a look** (needs linking). Check the look on the site, and note whether its body (feminine or masculine) is right. | |
| E6 | Close Examine. | |

## F · The fitting room

**Done (0.5.1):** F1 to F3.

| Id | Do this | DUMP |
| --- | --- | --- |
| F1 | In the plugin, open a look and press **Try it on**. Wait for the toast ("In the fitting room…"), then MARK what the fitting room shows: every piece, or which are missing. | yes, with the fitting room open |
| F1b | With the fitting room still open, open another look and press **Try it on** again. MARK whether the first look's pieces are gone. | |
| F2 | Close it. Then right-click any gear in your bags → **Try On**, the game's way. | yes |
| F3 | Close it. | |

## G · Shots

**Done (0.5.1):** G1 to G6 (2560×1440 with DLSS).

**You need:** linked to the site; one of your **public** looks worn; a quiet spot.

| Id | Do this | DUMP |
| --- | --- | --- |
| G1 | Open the look in the plugin → **Take a shot**. When the preview shows, type a caption and press **Send it to the look**. Check it on the look's page on the site. | |
| G2 | **Take another**, and while the HUD is hidden press **Scroll Lock** once. Note whether the HUD comes back by itself, and whether you had to press Scroll Lock again. | |
| G3 | **Take another** and **Discard** it. | |
| G4 | Hide the HUD yourself with Scroll Lock first, show it, then take one more shot and discard it. | |
| G5 | If your game runs above 1920 wide (1440p or 4K), take one more and note the size the plugin shows under the preview. Send it if you like. | |
| G6 | If dynamic resolution or upscaling (DLSS/FSR) is on in your graphics settings, take one with it on, then one with it off, and note it. | |

## H · Reading your wardrobe

**Done:** checked on the site.

**You need:** a summoning bell and your retainers; saddlebags (any place you can open them); an
alt on the same account if you have one.

| Id | Do this | DUMP |
| --- | --- | --- |
| H1 | Log in. Mark at once: `H1 logged in`. Open your bags and armoury chest. | |
| H2 | Open your chocobo saddlebags. | |
| H3 | At a summoning bell: open each retainer and their bags (entrust or withdraw items), one after the other. Mark each: `H3 retainer 1`, `H3 retainer 2`, … | yes, with a retainer's bags open |
| H4 | Move one gear piece from your bags to your armoury chest, then wait 15 seconds. | |
| H5 | Log out to the title screen and log in to your alt. Mark `H5 alt`. Open its bags. | |
| H6 | Log back to your main. Mark `H6 back on main`. | |

## I · The site, both ways

**Done (0.5.1):** I4, I5. **Again with 0.5.2:** I1 (at a glamour dresser this time), I2 (reworded), I3 (the sent look now shows).

**You need:** linked to the site (Settings → Account).

| Id | Do this | DUMP |
| --- | --- | --- |
| I1 | On the site, Closet → Plates: plan a look for plate 4. In the game, **at a glamour dresser** (not the Character window's Glamour Plate, which only applies plates), open Glamour Plates on plate 4 and press **Fill plate 4** in the panel beside it. Then **Save**. | yes, before saving |
| I2 | The site won't list a piece you already have, so: on the site, put a cheap piece you **don't** have on your shopping list (one sold on the market board). In the game, buy it, then `/gstudio send`. Note whether chat says it came off the list, and check the site. | |
| I3 | On the site, open a look → Send to game for plate 5. In the game, open the plugin (any way): it should be first under **Sent from the site**. At a glamour dresser, open Glamour Plates on plate 5, open the sent look in the plugin and press **Put on plate 5**, then **Save**. Check it's gone from Sent from the site. | yes, before saving |
| I4 | In the plugin's Plates, pick a plate that isn't one of your looks → **Keep it as a look**. Then **Keep it as a look** under What you're wearing. Check both on the site. | |
| I5 | Press **Send now** in the plugin's Closet. | |

## J · The panels beside the game's windows (0.4.0)

**You need:** linked to the site; an inn room (dresser and armoire); a summoning bell and a market
board (a city); a few looks of yours, one planned for a plate. Turn nothing off in Settings.

| Id | Do this | DUMP |
| --- | --- | --- |
| J1 | Open your glamour dresser. MARK what the panel beside it shows, and whether it sits right against the window. Drag the dresser around the screen, then to the right edge: MARK whether the panel follows and moves to the left side. | yes |
| J2 | In the dresser's panel, press **Plate it** on a ready look. MARK whether Glamour Plates opened by itself. Pick a plate: MARK whether the panel offers that look. | yes, with Glamour Plates open |
| J3 | Still in Glamour Plates: pick the plate you planned a look for. Press **Fill plate N** in the panel, then **Save** in the game. MARK what the panel shows after each. Then **Another look** → pick one, and pick a different plate: MARK whether the pick went away. | |
| J4 | Fold the panel (its arrow at the top right), close and open Glamour Plates again: MARK whether it stays folded. Open it again from the tab. | |
| J5 | Open your armoire. Press **Store** on one piece, then **Store all** if there are more. MARK what happened. | yes |
| J6 | At the dresser, start storing an outfit set (right-click a set piece → store as outfit). MARK what the panel says. Cancel. | yes |
| J7 | Open the Character window → Glamour Plate button. MARK the panel; press **Wear** on a plate. | |
| J8 | Open the fitting room with **Try it on** from any panel or the plugin, then use the panel's arrows twice. MARK whether each look shows whole. Try a piece on by hand from your bags, then MARK what the panel says. | yes |
| J9 | Examine someone. MARK the panel; press **Add … to the shopping list** if it shows. | yes |
| J10 | Dye a piece of gear from your bags (right-click → Dye). MARK the panel: does it name the right piece? | yes, with the dye window open |
| J11 | Hover gear in your bags so its tooltip shows: MARK the lines under it. Right-click it → **Glamour Studio**: try **Which looks use it** and **Add it to a look…** (then check the look on the site). | |
| J12 | At a summoning bell, open a retainer's bags. MARK the panel. | yes |
| J13 | At a market board, open it. In the panel press **Search** on a piece from your list. MARK whether it searched (or copied the name). Open a result, MARK whether the cheapest price shows. | yes, with results open |
| J14 | Open any shop that sells gear (a vendor or an exchange). MARK what the panel says. | yes |
| J15 | Hide the HUD (Scroll Lock) with the dresser open, then show it. MARK whether the panel hid with it. | |

## K · Make room for me (0.6.2)

**You need:** a glamour dresser with an outfit set's pieces loose in it (undyed: the plugin leaves
dyed ones), glamour prisms (one a piece), and free bag places (as many as the biggest set's loose
pieces). Start the recording (`/gstudio record`) before K1.

| Id | Do this | DUMP |
| --- | --- | --- |
| K1 | Open the dresser, and the plugin's Make room (or look at the panel beside the dresser). MARK what **Make room for me** says (sets, places, prisms, bag places). | yes |
| K2 | Press **Try it on one set**, read the question (it names the set), press **Try it**. Don't touch anything until it says it's done (or stops). MARK what it said, then check the dresser: is that set there as one outfit, its loose pieces gone, a prism spent per piece? | yes, when it's done |
| K3 | If it stopped, MARK the message and what's in your bags. If it worked, the button now says **Store … sets for me**: do it once more for all of them, and MARK how it ended. | yes |
| K4 | With duplicates in the dresser (Make room → Duplicates says how many): press **Take out …**, then **Take them out**. MARK what it said, and check your bags and the dresser. | yes, when it's done |

## Sending them

Send the saved files (one per section). Screenshots of anything odd help, and so does a note of
anything you didn't have (no outfit set, no alt) or skipped.
