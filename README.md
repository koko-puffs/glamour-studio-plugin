# Glamour Studio (FFXIV plugin)

A Dalamud plugin for [Glamour Studio](https://kokos-tools.com/glamour-studio): your looks on your
glamour plates, and your wardrobe on the site.

At a glamour dresser, open **Glamour Plates**, pick one of your looks in the plugin and press
**Put on plate 3**: every piece is found in your glamour dresser or armoire and the plate is filled,
ready for you to check and save. **Try it on** shows any look in the game's fitting room, wherever
you are.

Linked to your account on the site, it keeps your **closet** there up to date by itself: what's in
your glamour dresser, armoire, bags, armoury chest, saddlebags and retainers, your glamour plates and
what you're wearing. Looks you **Send to game** from the site wait in the plugin, and a plate (or your
outfit, or someone you examine) can be kept as a new look on the site.
On each character it hasn't seen before it **asks you first** whether to send that character at
all, and sends nothing of it until you say yes; what's sent stays private on the site unless you
choose to show it there.

Most of it is right beside the game's own windows: open the glamour dresser, Glamour Plates, the
armoire, the fitting room, Examine, a retainer's bags or the market board, and a small panel sits
next to it with what you need there (Fill plate, Plate it, Store, Wear, your shopping list).
Gear's tooltips and right-click menu get a few Glamour Studio lines too.

It also fills plates with the looks you **planned** for them on the site, lists the public looks you
could **wear now**, ticks off your **shopping list**, helps you **make room** in your dresser, links
plates to **gearsets**, and takes **shots** of a look for its page on the site.

## Installing

1. In the game, open `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Add `https://www.kokos-tools.com/dalamud/repo.json`, tick it, and **Save and Close**.
   (Already there for Housing Studio or Raid Planner? Skip this.)
3. Open `/xlplugins`, search for **Glamour Studio** and install it.
4. Type `/gstudio` to open it.

## Using it

1. Link your account: on the site, Settings → Glamour Studio → In-game plugin → **Make a token**,
   and paste it in the plugin's Settings → Account.
2. Open your glamour dresser and armoire once (at an inn): the game only shows them then, and the
   plugin remembers them.
3. At a glamour dresser, open **Glamour Plates** and pick a plate. The panel beside it shows the
   look planned for it (or pick another) and **Fill plate**. Check it in the game's window, then
   press **Save** there.

Closet says what the plugin has read and when, and what to open so it can read the rest.

Something not working? Settings → Making a report records what you do in the game (or
`/gstudio record`), to copy or save and send along.

**Please note:** plugins are against Square Enix's terms of service. This one reads what the game
already shows you, and only changes things when you press a button for it: a plate (you save it
yourself), a gearset's plate, a piece into the armoire.
Whether to use it is your call.

## Building it

You need the .NET 10 SDK and Dalamud (installed through XIVLauncher, or unpacked with
`DALAMUD_HOME` pointing at it).

```sh
dotnet test GlamourStudio.Tests
dotnet build GlamourStudio.sln -c Release
```

Releasing: push a tag like `v0.1.0` (details in [DETAILS.md](DETAILS.md#releasing)).

## More detail

[DETAILS.md](DETAILS.md): every screen and setting, how plates are filled, what's sent to the site,
how the code is laid out, and the release notes.

## Credits and licence

Built on what the Glamaholic plugin and FFXIVClientStructs found about the game's glamour
plates, and on Housing Studio's and Raid Planner's plugins. Fonts: Syne, DM Sans and DM Mono (SIL
Open Font License). Icons drawn from Lucide (ISC). Licensed under the GNU AGPL-3.0 (see `LICENSE`).
