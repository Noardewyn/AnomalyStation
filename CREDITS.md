# Credits and third-party licenses

Anomaly Station is a port of Space Station 13 to first-person 3D. It stands on the work of many people; this file
says whose work it uses and under which license.

## This project

- **Code:** Copyright (C) 2026 Noardewyn and the Anomaly Station contributors. Licensed under the
  [GNU Affero General Public License v3](LICENSE). Anyone who runs a modified server must offer its players the source
  of that version (AGPL section 13).
- **Art and media** in this repository (3D models, textures, interface images, screenshots): licensed under
  [CC BY-SA 3.0](LICENSE-ASSETS); see that file for exactly which folders it covers.

## /tg/station

Most of the game's logic is translated from [/tg/station](https://github.com/tgstation/tgstation) (snapshot
`1d99bd7f`), copyright the tgstation contributors. Each translated file names its tg source.

- **Code:** GNU AGPL v3 (tg code after 2014-12-31; earlier code GNU GPL v3). Parts of tgui are under MIT, as marked
  in tg. Translated code stays under the AGPL.
- **Icons, sounds and fonts** are not stored in this repository: the build copies them from a tgstation clone into
  `godot/assets/tg/`, and release builds ship them. tg puts its assets under
  [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) unless indicated otherwise. Some sounds name other
  licenses in the notes of tg's `sound/` tree (`attribution*.txt`, `credits*.txt`, `license.txt`); the build records each
  sound's license in `godot/assets/tg/sound/index.json`. Builds carry tg's license notes beside the sounds
  (`assets/tg/sound/**/attribution*.txt` and the like), which name each sound's authors. The sounds whose license forbids
  commercial use are kept apart in `assets/tg/sound-nc/`: the game is distributed free, and a build made with
  `ANOMALY_SKIP_NC=1` leaves them out, as any commercial release must. No sound without a license is shipped: tg's lobby
  tracks `title1.mod`, `title2.ogg` and `title3.ogg`, `music/elevator/robocop-short.ogg`, `announcer/alarm/clownops.ogg`
  and `announcer/alarm/airraid.ogg` are left out.
- Content whose license forbids commercial use is not ported: code tg marks as derived from Goonstation
  (CC BY-NC-SA 3.0), and Cards Against Spess (`code/modules/cards/deck/cas.dm`, a parody of Cards Against Humanity
  with CC BY-NC-SA 2.0 card texts).

### Fonts (copied from tg's `interface/fonts` and `html/font-awesome`)

| Font | Author | License |
|---|---|---|
| Grand9K Pixel | Jayvee Enaguas | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| Pixellari | Zacchary Dempsey-Plante | free for commercial use (author's site) |
| Tiny Unicode | Jakob Riedle (DuffsDevice) | free for commercial use (author's site) |
| VCR OSD Mono | Riciery Leal (mrmanet) | free for all use (author) |
| Spess Font | MTandi | made for /tg/station |
| Font Awesome Free 6.7.2 | Fonticons, Inc. | fonts SIL OFL 1.1, icons CC BY 4.0, code MIT ([license](https://fontawesome.com/license/free)) |

## Engine and libraries

| Component | License | Used in |
|---|---|---|
| [Godot Engine](https://godotengine.org/license) and its [third-party components](https://github.com/godotengine/godot/blob/master/COPYRIGHT.txt) | MIT | client |
| [.NET](https://github.com/dotnet/runtime) | MIT | everything |
| [LiteNetLib](https://github.com/RevenantX/LiteNetLib) | MIT | client, server |
| [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET) and [Dear ImGui](https://github.com/ocornut/imgui) | MIT | client dev tools |
| [ONNX Runtime](https://github.com/microsoft/onnxruntime) | MIT | client speech |
| [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) | Apache-2.0 | client speech |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) | MIT | server |
| [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw) | Apache-2.0 | server |
| [SQLite](https://sqlite.org/copyright.html) | public domain | server |
| [rust-g](https://github.com/tgstation/rust-g) 7.0.0 (map generation, rewritten in C#) and the dbpnoise crate | MIT | server |
| Roslyn (Microsoft.CodeAnalysis), xUnit, Microsoft.NET.Test.Sdk | MIT / Apache-2.0 / MIT | build tools and tests only |

## Speech and translation models

Downloaded by the client only when the player turns the option on (Settings > Speech); not shipped with the game.

| Model | Author | License |
|---|---|---|
| Supertonic 3 (sherpa-onnx int8 export) | Supertone | OpenRAIL-M (its LICENSE is downloaded beside it) |
| Whisper base (sherpa-onnx int8 export) | OpenAI | MIT |
| OPUS-MT translation models (ONNX exports by Xenova and TigreGotico) | Helsinki-NLP | CC BY 4.0 |

## Thanks

To Exadv1, who created Space Station 13, and to everyone who has worked on its codebases since.
