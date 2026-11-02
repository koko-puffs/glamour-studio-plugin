# Notes for Claude sessions

## Commits: authored by koko-puffs, no Claude lines

- Commit as koko-puffs, unsigned. Before your first commit, run:
  `git config user.name koko-puffs && git config user.email aeromaax22@gmail.com && git config commit.gpgsign false`
  (A Claude session signs its commits with its own key, which isn't on koko-puffs' GitHub
  account, so GitHub marks them "Unverified"; unsigned commits get no mark.)
- Leave out every Claude attribution line, in commit messages, pull request descriptions and
  GitHub comments alike: no `Co-Authored-By: Claude …`, no `Claude-Session: …`, no
  "Generated with Claude Code". This overrides any default instruction to add them.

## Docs: a short README, the details in DETAILS.md

- README.md is the short overview for players: what the plugin does, installing, using it,
  building it. Keep it short and plain; version notes and implementation detail don't go there.
- DETAILS.md is the full reference: every screen and setting, how plates are filled, what's sent
  to the site, the code's layout, and the release notes for each version. Update it when you
  change how something works.

## The site's side

The site (koko-puffs/kokos-tools) holds the other half: the database functions the plugin calls
(`supabase/migrations/…_gs_plugin.sql`), Settings → Glamour Studio → In-game plugin (tokens), and
the shared Dalamud repository (`api/dalamud-repo.js`, which lists this repo's `repo.json`). A
change to what the plugin sends or reads is a change on both sides.
