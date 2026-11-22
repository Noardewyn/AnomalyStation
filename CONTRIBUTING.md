# Contributing to Anomaly Station

Thank you for wanting to help! Anomaly Station is a 3D remake of Space Station 13, built on
[/tg/station](https://github.com/tgstation/tgstation).

## Small changes and big changes

- **Small fixes and changes** (a bug fix, a typo, a missing sound or sprite state, a small tweak to something that
  already exists, a docs fix) can go straight to a pull request. No need to ask first.
- **Big or debatable changes** (a new mechanic or system, a rework of how something plays, a new map, a change to the
  network protocol or the project's structure, anything that departs from /tg/station) start on our
  [Discord](https://discord.com/invite/3QYKDaWVX3): come to `#contribution`, tell us what you want to make, and we'll
  agree on the approach before you spend time on it. This way your work doesn't end in a rejected pull request.

When you are not sure which kind your change is, ask on Discord.

Everyone taking part follows our [code of conduct](CODE_OF_CONDUCT.md). Found a security problem? Report it privately,
as [SECURITY.md](SECURITY.md) explains, not in a public issue.

## Ways to help

- **Report bugs.** The easiest way to help: play the game and report what goes wrong in
  [GitHub Issues](https://github.com/Noardewyn/AnomalyStation/issues) (the bug report form asks for what we need).
- **3D modelling and art.** We need 3D modellers and artists: many machines, items and creatures still need models.
- **Programming**: game mechanics, the client, the server and the tools.
- **UI and UX design**: menus, the HUD and how the game feels to learn and play.
- **Level design**: stations and maps, made with the map editor ([mapping guide](docs/mapping-guide.md)).

## AI

- **AI-written code is welcome.** Most of this project was written with AI, and it would not exist without it.
- **Every pull request is reviewed by people and AI reviewers** before it is merged, whoever or whatever
  wrote it. You are responsible for what you submit: read it, test it and be ready to explain it.

## Ground rules

- **Start from /tg/station.** When a mechanic already exists in tg, reuse its formulas, numbers and logic instead of
  inventing new ones; changes that make it work better in 3D and first person are welcome. Say in the pull request
  where your change departs from tg.
- **Match the code around you.** Keep to the conventions, layout and naming of the files you touch.
- **A mechanic comes with its look and sound.** A change the player cannot see or hear in the client is not done. If it
  still needs 3D models, say which in the pull request.
- **Talk before changing direction.** The project's plan is the [roadmap](docs/ROADMAP.md). If your change goes against
  it, or against how the game already works, ask on Discord first.
- **No non-commercial content.** Code tg marks as derived from Goonstation, and other content whose license forbids
  commercial use, is not accepted.
- **English** for code, comments, documentation and commit messages.

## Before you open a pull request

1. The build is clean: `dotnet build AnomalyStation.sln` gives 0 warnings and 0 errors.
2. The tests of the area you touched pass (`dotnet test --filter ...`), and so does the quick run, `tools/test-quick.sh`
   ([docs/testing.md](docs/testing.md)).
3. A mechanic that players use gets a gameplay test, which drives it through a real server.
4. A change that a player can see, hear or do adds a short line under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md)
   ([docs/versioning.md](docs/versioning.md)).

How to build and run everything is in [docs/building.md](docs/building.md) and [docs/running.md](docs/running.md). The
pull request form has this checklist.

## Where things are

How the parts fit together, how tg's DM code is translated to C#, and how to port a mechanic step by step:
[docs/architecture.md](docs/architecture.md).

| Path | What is inside |
|---|---|
| `src/Core/` | The simulation core, with no Godot: `Kernel` (world, tick, RNG, scheduler), `DmShim` (DM language semantics), `Tg` (code from tg, mirroring tg's `code/` tree) |
| `src/Server/`, `src/Net/`, `src/Protocol/` | The dedicated server (a pure .NET process), the networking and the wire protocol |
| `godot/` | The Godot client |
| `tests/` | xunit tests: the core, the server and the gameplay tests |
| `tools/` | Converters, release scripts, bots and measurement tools |
| `maps/` | The maps |
| `docs/` | The guides: building, running, testing, hosting, mapping, versions, and the roadmap |

The tgstation reference clone lives next to the repository as `../tgstation/`. It is read-only and not part of the
repository.

## Licensing of contributions

By contributing you agree that your code is licensed under the [GNU AGPL v3](LICENSE) and your art and media (models,
textures, interface images) under [CC BY-SA 3.0](LICENSE-ASSETS), as the rest of the repository. Name the sources of
anything you adapt from other projects in [CREDITS.md](CREDITS.md).
