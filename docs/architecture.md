# How Anomaly Station works

A map of the code for programmers: what the parts are, how a piece of /tg/station becomes part of the game, and where to
start when you port a mechanic.

## The big idea

Space Station 13 runs on BYOND, and its game logic is written in BYOND's DM language. /tg/station is about a million
lines of DM. Rewriting all of it by hand would take years and change how the game plays.

Anomaly Station takes another route: **tg's DM code is translated into C#, file by file and proc by proc**, keeping its
formulas, numbers and order of events. A small library (the "DM shim") gives the translated code DM's semantics: DM's
lists, its truthiness, its 24-bit bit operations, its `sleep` and `spawn`. On top of that runs a dedicated .NET server,
and players see the world through a first-person 3D client built in Godot.

```
 ../tgstation (DM code, sprites, sounds, maps)
        |
        |  translated by hand and by generators          converted by tools/convert-tg.sh
        v                                                         v
 src/Core  (the simulation: kernel + DM shim + translated tg)   godot/assets/tg (PNG, OGG)   maps/ (.as3dmap)
        |
        v
 src/Server  (runs the world, ticks it, replicates it)  <--- UDP (LiteNetLib) --->  godot/ (the 3D client)
```

## The parts

| Path | What it is |
|---|---|
| `src/Core/Kernel/` | The world, the tick loop, the deterministic random numbers, the scheduler, and `DmTask`, which runs procs that wait |
| `src/Core/DmShim/` | DM's language and builtins in C#: `Dm.*` (lists, text, math, types, conversions), `DmList`, appearances, the type registry |
| `src/Core/Tg/` | The translated tg code. Its folders mirror tg's `code/` tree: `code/game/machinery/igniter.dm` becomes `src/Core/Tg/game/machinery/igniter.cs` |
| `src/Core/Tg/Generated/` | Code generated from tg: every `#define` as a C# constant, every type's variables and their defaults. Not edited by hand |
| `src/Protocol/`, `src/Net/` | The network messages and the transport (LiteNetLib over UDP) |
| `src/Server/` | The dedicated server: a pure .NET process that owns the world, takes the players' input and sends each player what they can see |
| `godot/` | The Godot client: rendering, sound, input, the HUD and the tgui windows. `godot/scripts/Shared/` is client logic with no Godot dependency, so the tests can run it |
| `tools/` | The converters (icons, sounds, maps, defines, types), bots, release scripts and Roslyn analyzers |
| `tests/` | The xunit tests ([testing.md](testing.md)) |

The core has no dependency on Godot. The server and the tests run it without any graphics.

## How tg code is translated

Each translated file starts with the tg file it comes from, and each type and proc carries a `// tg: <file>:<line>`
comment pointing at its DM original:

```csharp
// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/igniter.dm

// tg: code/game/machinery/igniter.dm:1
public partial class obj_machinery_igniter : obj_machinery
{
    // tg: code/game/machinery/igniter.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        register_context();
        return dot;
    }
}
```

The main conventions:

- **Names stay tg's.** A type `/obj/machinery/igniter` is the class `obj_machinery_igniter`; procs and vars keep their DM
  names (`update_appearance`, `use_power`). A tg type spread over several `.dm` files is one `partial class` with a part
  in each mirror file.
- **One DM statement per C# line, in DM order.** Someone comparing the two files side by side should be able to follow
  along.
- **DM semantics go through the shim.** `if(x)` becomes `if (Dm.truthy(x))`, `a == b` on DM values becomes
  `Dm.eq(a, b)`, `for(var/obj/item/I in user)` becomes `foreach (obj_item I in Dm.each<obj_item>(user))`.
- **Procs that wait are `async`.** A DM proc that can `sleep`, call `do_after` or open a dialog becomes an `async` method
  returning `DmTask`, and its callers `await` it. `DmTask` is not a .NET `Task`: it uses no threads, and the world's
  scheduler resumes it on a later tick, so the simulation stays deterministic. The analyzers in
  `tools/Anomaly.Analyzers` catch the common mistakes at build time: a `DmTask` that is dropped or stored instead of awaited, or a proc that awaits a .NET `Task`.
- **Unported parts are marked.** A branch that is not translated yet calls `Dm.NotTranslated("code/path.dm:123", ...)`:
  it throws in tests and logs once in a running game.
- **Player-visible text** goes through `I18n.T(...)`, so it can be translated later.
- **Departures are allowed when 3D needs them**, and are written down. Changes that make a mechanic work in first
  person (aiming, movement, what a sprite becomes in 3D) are fine; changing tg's formulas or balance for its own sake is not.

## From the server to the screen

1. **The server ticks the world.** tg's subsystems (atmospherics, machines, mobs, the shuttle) run on the server's tick,
   as tg's master controller runs them.
2. **The server replicates what each player sees.** Every object has an appearance (tg's icon, icon state, overlays,
   color, direction). The server sends each client the appearances and positions of the things in its view, and
   changes as they happen.
3. **The client draws them in 3D.** Walls and floors become 3D geometry. Machines and furniture that have a 3D model in
   `godot/assets/models/` are drawn with it; the model plays the tg icon state the server sent. Items on the floor
   stay 2D billboards of tg's sprites, and items in your hands become extruded sprites. Anything without a model yet is
   drawn as its tg sprite, so nothing is invisible while the art catches up.
4. **The client sends input.** Movement, clicks on objects (tg's left and right click), the radial menu, the tgui
   windows' actions. The server runs them through tg's own click and interaction code.

Movement is predicted on the client and corrected by the server, so walking feels immediate on a normal connection.

## Assets and maps

- **Sprites and sounds** are converted from the tgstation clone by `tools/convert-tg.sh` into `godot/assets/tg/` (not
  committed). They keep their licenses ([CREDITS.md](../CREDITS.md)).
- **3D models** live in `godot/assets/models/`: a `.blend` source (in Git LFS) and its `.glb` export per object, usually
  with notes (`model_info.json`, `source_states.json`) on the tg icon states it was made from.
- **Maps** are tg's `.dmm` maps converted to the project's `.as3dmap` format, checked in under `maps/`, and edited with
  the built-in map editor ([mapping-guide.md](mapping-guide.md)).

## Porting a mechanic

1. **Find it in tg.** Read the DM code in `../tgstation/code/`, including what calls it and the signals it sends.
2. **Check what exists.** Search `src/Core/Tg/` for the mirror file and the type: a skeleton or part of it may already be
   there, with `Dm.NotTranslated` where the work is left.
3. **Translate it** into the mirror file, following the conventions above and the code around it.
4. **Make it visible and audible.** The icon states, overlays and animations that show its state, and its sounds, come
   with it. If it needs new 3D models, say which in your pull request.
5. **Test it.** Unit tests in `tests/Anomaly.Core.Tests`, and for a mechanic players use, a gameplay test in
   `tests/Anomaly.Server.Tests` that drives it through a real server.
6. **Try it in the game** with a local server ([running.md](running.md)), and add a line to the changelog.

## Going deeper

Most of the project was written with AI coding agents, and the detailed specs they work from (the full translation
rules, the waiting model, the network protocol, the object model, the register of deviations from tg) are kept in
`docs/ai/`. They are written for agents and are dense, so they are not required reading; look there when you want to
know exactly why something works the way it does.
