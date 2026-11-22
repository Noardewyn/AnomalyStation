# Building Anomaly Station

How to build the game from source, run its tests and make release builds. How to start what you built is in
[running.md](running.md).

## What you need

| Tool | Version | Notes |
|---|---|---|
| Git with [Git LFS](https://git-lfs.com/) | any recent | the `.blend` model sources are stored in LFS: run `git lfs install` once before cloning |
| .NET SDK | 10 | `dotnet --version` |
| Godot | 4.7.2 **mono** (the .NET build) | see [Godot on each OS](#godot-on-each-os) |
| tgstation clone | commit `1d99bd7f` | next to this repository, as `../tgstation`: see [Getting tgstation](#getting-tgstation) |

On Windows, run the `.sh` scripts in Git Bash (it comes with Git for Windows), not in cmd or PowerShell.

### Hardware

| | Enough to build and play | Enough to run the full test suites |
|---|---|---|
| Memory | 16 GB | 32 GB or more ([testing.md](testing.md#memory)) |
| Disk | about 15 GB free (the sources, tg, the converted assets, Godot's import cache and the build outputs) | the same |
| GPU | one that runs Godot's Forward+ renderer (Vulkan, Direct3D 12 or Metal) | |

A local server on MetaStation holds about 1.5 GB of memory; the client runs beside it on the same machine.

### Getting tgstation

The game's logic, sprites, sounds and maps come from [/tg/station](https://github.com/tgstation/tgstation), pinned at one
commit. The tools read it from `../tgstation`, next to this repository. You don't need tg's whole history (about 3.5 GB);
this fetches only the one commit (about 600 MB):

```bash
git init ../tgstation
git -C ../tgstation fetch --depth 1 https://github.com/tgstation/tgstation.git 1d99bd7f599fcd687fd563cbe01370d0b020e46f
git -C ../tgstation checkout FETCH_HEAD
```

Treat this clone as read-only: nothing of it is committed here, the build only reads from it. `ANOMALY_TGSTATION` points the
scripts at a clone somewhere else.

### Godot on each OS

The scripts look for `godot-mono` on `PATH`; `GODOT=/path/to/godot` points them at another binary.

- **macOS:** `brew install --cask godot-mono` puts `godot-mono` on `PATH`.
- **Windows:** download the **.NET** build of Godot 4.7.2 and unzip it, for example to `C:\Godot`. Run the console
  executable from Git Bash, or set `GODOT` for the scripts:

  ```bash
  export GODOT="/c/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
  "$GODOT" --path godot -- --connect=127.0.0.1:7777 --name=YourName
  ```

  Use the `_mono` executable from the zip: a Godot installed without .NET (for example the non-.NET package from a
  package manager) cannot load the game's C# assemblies.
- **Linux:** download the .NET build of Godot 4.7.2 and link its binary as `godot-mono` somewhere on `PATH`, or set `GODOT`.

Wherever the docs write `godot-mono`, use your own Godot command.

## Build

```bash
tools/convert-tg.sh                              # tg's icons and sounds into godot/assets/tg, the maps when maps/ is empty, the Godot import
dotnet build AnomalyStation.sln -c Release       # the core, the server, the tools and the tests (0 warnings expected)
dotnet build godot/Anomaly.Client.csproj            # the client
```

`tools/convert-tg.sh` only converts: run it once after cloning and again when the tgstation snapshot changes. It never
writes over the checked-in maps, which are edited with the map editor; `tools/convert-maps.sh --force` converts tg's
default maps over them (rarely wanted: git shows what it changed).

## Smoke check (optional)

```bash
godot/tools/smoke.sh                                  # import, client build, headless runs on a scripted world and on MetaStation
godot/tools/smoke.sh shot.png                         # also opens a window for a few seconds and saves a first-person screenshot
SMOKE_CONNECT=127.0.0.1:7777 godot/tools/smoke.sh     # also joins a running server for a few seconds
```

An automatic check that the client starts, draws the world and plays sounds: it fails on any Godot error or a missing log
line and prints `smoke: OK` at the end. It needs the data `tools/convert-tg.sh` converted and converts nothing itself.

## Test

```bash
tools/test-quick.sh                         # everything but the MetaStation tests: the run before a commit
dotnet test tests/Anomaly.Server.Tests         # the server, the protocol, the client logic, MetaStation scenarios
dotnet test tests/Anomaly.Core.Tests           # the core and the translated tg code, including tg's own unit tests
dotnet test AnomalyStation.sln              # everything, including the analyzer tests
dotnet test tests/Anomaly.Core.Tests --filter "FullyQualifiedName~Engineering"   # one area
```

Quick and full runs, the Meta tests and their timings are in [testing.md](testing.md).

Determinism checks:
- `dotnet run --project tools/Anomaly.Tools.ToyWorld -c Release -- --twice` runs the toy world twice and compares the hashes.
- `--record run.log`, then `--replay run.log` on the server replays a run and checks the final hash.

## Release builds

Optimized, self-contained builds (no .NET or Godot needed where they run), zipped into the git-ignored `dist/`. Any host
builds any target. Both need the .NET 10 SDK and tg's converted data (made from `../tgstation` when missing); the client
also needs Godot's export templates (in the Godot editor: Editor > Manage Export Templates > Download and Install).

```bash
tools/build-release.sh windows    # the server and the client for Windows
tools/build-release.sh macos      # the server and the client for macOS
tools/build-release.sh all        # all of them
```

It takes a few minutes and leaves in `dist/`:

| Zip | What it is |
|---|---|
| `AnomalyStation-server-win-x64.zip` | the Windows server |
| `AnomalyStation-server-osx-arm64.zip` | the macOS server (Apple silicon; `ANOMALY_MAC_ARCH=x86_64 tools/build-release.sh macos` for an Intel Mac) |
| `AnomalyStation-server-linux-x64.zip` | the Linux server, for a VPS ([hosting.md](hosting.md)) |
| `AnomalyStation-client-win-x64.zip` | the Windows client |
| `AnomalyStation-client-macos-universal.zip` | the macOS client (Apple silicon and Intel) |
| `AnomalyStation-client-linux-x64.zip` | the Linux x64 client |

The single-target scripts:

| Command | What it builds |
|---|---|
| `tools/build-release.sh` | the server and the client for this host (`windows`, `macos`, `linux` or `all` names a target) |
| `tools/build-server.sh win-x64` | the server (`osx-arm64`, `osx-x64`, `linux-x64`) with its data: `dist/AnomalyStation-server-<rid>.zip`; its `config/server.txt` has `REALTIME`, `BIND_ADDRESS 0.0.0.0` and `MAP metastation`; the admins go in `config/admins.txt` |
| `tools/deploy-server.sh root@HOST` | the Linux server built, uploaded to a VPS and installed or updated there as a systemd service: [hosting.md](hosting.md) |
| `godot/tools/export-windows.sh` | the Windows x64 client: `dist/AnomalyStation-client-win-x64.zip`, run sheet `godot/tools/README-windows.txt` |
| `godot/tools/export-macos.sh` | the macOS client, universal: `dist/AnomalyStation-client-macos-universal.zip`, `Anomaly Station.app` with its data beside it, run sheet `godot/tools/README-macos.txt` |
| `godot/tools/export-linux.sh` | the Linux x64 client: `dist/AnomalyStation-client-linux-x64.zip`, run sheet `godot/tools/README-linux.txt` |

`GODOT` names the Godot binary when it is not found, `GODOT_TEMPLATES` the templates' folder, `ANOMALY_TGSTATION` the
tgstation clone.

How to start a release build is in [running.md](running.md#running-a-release-build). Versions and the changelog are in
[versioning.md](versioning.md).
