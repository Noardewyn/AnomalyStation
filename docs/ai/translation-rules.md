# Translation rules: tg DM to C#

Status: **v1.4**, 2026-10-08. v1.4 (the M3 contracts step) makes var blocks generated from M3 on (10.2: translators add procs in partial classes beside the generated part; hand-written var blocks stay only for the classes of M0-M2, under the conformance test). v1.3 (2026-10-07) settles the rule gaps of M1's client layer: `new` only where C# hides a member (7.1), the engine's creation without `New` (10.1), the `__gap` suffix (2.5), typed `as anything` loop vars that the body type-tests (6.5), two more `.`-on-runtime sites (9.3) and the engine's call of `client/New` as a boundary (9.2). v1.2 (2026-10-06) writes the lead's decisions on the M0 pilot findings into the rules (labelled loops, proc references as text, file-local `#define`s, inline `ASSERT`, `stack_trace` at the caller's location, partial and skeleton mirror files, `/world` procs, internal `TgState` fields, ported tg unit tests, and the smaller gaps listed in the commit history). Binding for every file under `src/Core/Tg/` and for the shim APIs these rules name. v1 (2026-10-05) replaced v0 after the wave-0 review; the review findings it settles are listed per rule in the commit history, not here. Items marked **[C0]** were presented to the owner at checkpoint C0 (D94) and confirmed there as they are (D95, 2026-10-07).

**Goal.** Two translators who apply these rules to the same `.dm` file emit the same C#, token for token apart from whitespace and comments. Every rule therefore names one form. Where a rule leaves a choice, that is a bug in this document: report it to the lead instead of choosing.

**Principle (D83, D86).** Translate, do not redesign. Keep the structure, the formulas, the numbers, the order of evaluation and the order of events of the tg procedure. One DM statement becomes one C# statement wherever C# allows it. Change only what C# or the 3D boundary forces, and say so in a comment.

---

## 0. Ground rules

### 0.1 Which document wins

1. `docs/ai/DECISIONS.md` (owner decisions) and `docs/ROADMAP.md`.
2. This file and the specs in `docs/ai/specs/*.md`. Each spec owns its subject and this file only points to it:
   - `docs/ai/specs/waiting-model.md`: waiting procs, colouring, the call forms `await` / `Waiting.Detach` / `Waiting.NoSleep`, `set waitfor = FALSE`, `sleep`, `spawn`, the tick order.
   - `docs/ai/specs/object-model.md`: object identity, `Dm.@new`, var defaults and prototypes, the world object registry, state hashing, the type registry, per-world global state, where global procs live.
   - `docs/ai/specs/presentation-seam.md`: messages, sounds, appearance, light.
   - `docs/ai/specs/move-chain.md`: the engine contract of `loc`, `contents`, `Move` and the exact order of the movement signals.
   A conflict between two of these is a bug: stop and ask the lead.
3. `docs/ai/specs/m0-reading/*.md` (reading-pass reports): reference for tg facts (order of events, file:line, hazards). **Their C# sketches are non-binding and superseded by 1 and 2**: class and member names, PascalCase or camelCase, properties instead of fields, `double` time, `Dm.qdel` / `Dm.SendSignal` / `World.New`, `DMList`, `DM.*` and the namespaces they mention.
4. Generated tables (`docs/ai/specs/generated/`), which are inputs, not opinions: `slots.tsv` (every slot's C# signature, 7.1; `DmTask` exactly when the slot suspends its callers), `colored-slots.tsv` (the colouring behind it: `colored`, `suspends`, `waitfor_false`, the witness chains), `nosleep-sites.tsv` (the `Waiting.NoSleep` sites) and `sleeping-new.tsv` (the `New` definitions that may suspend their creator, 10.1), all produced by `tools/coloring` and explained in `docs/ai/specs/colored-procs.md`.

### 0.2 Arbiters for DM behaviour

In this order: the BYOND reference (complete offline copy: `curl https://www.byond.com/docs/ref/info.html`, one HTML page with an anchor per entry, for example `<a name=/proc/round>`), then the OpenDream sources (`raw.githubusercontent.com/OpenDreamProject/OpenDream/master/...`), then tg's own comments and usage. Where the reference is silent and OpenDream contradicts behaviour that tg documents for BYOND, tg wins (example: `round(A, B)` at exact halves, section 4.6). Every settled behaviour is pinned by a conformance test in `tests/Anomaly.Core.Tests/Dm/` (the behaviours the wave-1 contracts package pinned: `tests/Anomaly.Core.Tests/Contracts/`) with a line in `docs/ai/specs/dm-conformance.md` (one section per DmShim package). v0's "[verify]" markers are all settled in this version.

### 0.3 C0 confirmation items [C0] (confirmed, D95)

These choices are made by the assistant under D94 and need the owner's confirmation at C0. Translation proceeds on them meanwhile.

1. **Runtime errors unwind to isolation boundaries** instead of ending only the failing proc (section 9, `docs/ai/DEVIATIONS.md` DV1).
2. **Direct object references plus world ids** instead of D45's handles (int + generation): C# references, `dm_id` from `World.AllocateId()` for replication and `REF()` (`docs/ai/specs/object-model.md`).
3. **Type defaults are generated C# (constructors and a pre-New hook)**, not D45/D19 YAML/JSON prototypes. Data-only content packs (D19) can later override prototype values; they never add code.
4. **Suspended procs are not serializable** (D31 waived for sleeping procs; `docs/ai/specs/waiting-model.md`).
5. **Localization model**: core text stays English; player-visible text is marked with `I18n.T(template, args)` (section 5.3); the client localizes from (template, args). The transport of keys to the client is designed in M9.
6. **Text length in UTF-16 code units** instead of UTF-8 bytes (DV4).

### 0.4 Status of the names used here

Appendix C lists every shim, Kernel and Tg-infrastructure name these rules use, with its owner and status (exists, or planned for a later wave). A translator never invents a helper name: if a needed helper is missing from Appendix C, ask the lead.

---

## 1. Files, headers, namespaces, placement

**1.1 One C# file per `.dm` file**, path mirrored without the `code/` prefix, extension `.cs`: `code/datums/datum.dm` -> `src/Core/Tg/datums/datum.cs`. A DM type spread over several `.dm` files is one `partial class` with one part per mirrored file. Generated files live in `src/Core/Tg/Generated/` with the `.g.cs` suffix. Hand-written engine ("BYOND builtin") parts of a type (for example the `loc` setter of `/atom`) live in `src/Core/Tg/Builtin/<class>.builtin.cs` as another part of the same class.

**1.2 Header and usings.** Every translated file starts with exactly this block (file path adjusted), then the file-scoped namespace:

```csharp
// Translated from tgstation (snapshot 1d99bd7f): code/datums/datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;
```

**Bare or qualified.** What has a DM spelling is written bare, as in DM: the BYOND builtin constants and objects (`TRUE`, `FALSE`, `NORTH`, `world`), every BYOND builtin proc whose C# call keeps the DM call shape (same name, same arguments: `sleep`, `spawn`, `prob`, `rand`, `pick`, `round`, `length`, `findtext`, `num2text`, `istype(x, T)`, `typesof`, `animate`, `get_step`, ..., the full list is in Appendix C), the `#define` constants, and tg's global procs and hand-expanded macros (section 7.8). What has no DM spelling, or whose C# shape differs from the DM call, is the port's machinery and is always written qualified with its class: `Dm.truthy(x)`, `Dm.eq(a, b)`, `Dm.list(...)`, `Dm.text(...)`, `Dm.@new<T>(...)`, `Dm.Call(...)`, `Dm.initial(...)`, `Waiting.Detach(...)`, `I18n.T(...)`. When a bare name does not compile because a member, local or parameter of the same name hides it (CS1955 or CS0149, for example a tg var named `range` or a parameter named `length`), that call is written qualified (`Builtins.range(5, this)`, `Dm.length(L)`); this is the only exception.

**1.3 Source comments.** Before each translated type, proc, var block and significant var: one `// tg: <path>:<line>` comment with the DM definition's location in the snapshot (`// tg: code/datums/signals.dm:16`). tg's own doc comments are kept as they are (they are English already).

**1.4 Not translated yet.** A proc or block that is not translated yet is the statement `Dm.NotTranslated("code/path.dm:123", "proc_name");` (Appendix C: it throws in tests and logs once in a running world, then returns). A proc whose whole body is not translated is that statement followed, when the method returns a value, by `return default;` (the C# default of the return type, of its `T` for a `DmTask<T>` slot; null for `object`). A not-translated **block** inside a translated proc whose every DM path returns is that statement followed by the return the proc would take (`return null;` for an `object` or `DmTask<object>` slot, `return default;` for another return type, `return;` for `void` and `DmTask`), so that control does not fall through into the code after the block; a block that falls through in DM is the statement alone. Never leave a silent empty body. A mechanic counts as translated only when no `Dm.NotTranslated` is reachable from it.

**Deferred leaves (W3.0, lead decision W3 G1).** A leaf proc or block whose effects nothing in the current milestone reads (a statistics record, a smoothing queue, a database round id) may instead be `Dm.Deferred("code/path.dm:123", "proc_name: why");`, with the same returns after it as `Dm.NotTranslated`. It never throws: it counts the call in the world (`DmWorldState.DeferredCount`, `DeferredSites`, for tests), writes one line to the host log for the first call at each site and returns. It is the exception, not a second way to postpone: every use is listed in `docs/ai/specs/not-translated.md` with the milestone that translates it, and a leaf whose effect any code or test of the milestone reads stays `Dm.NotTranslated` (or is translated). An inert stub subsystem (`docs/ai/specs/wave3-ownership.md`) is the type-level counterpart: real `[DmPath]`, `name`, `init_stage` and `dependencies`, `ss_flags = SS_NO_INIT | SS_NO_FIRE`, and a `// STUB(Mx): translated in Mx (...)` line.

```
DM:  if(datum_flags & DF_VAR_EDITED)                                          (datums/callback.dm:113-116; admin tooling)
         if(usr != GLOB.AdminProcCallHandler && !(usr && usr?.client?.ckey))
             return HandleUserlessProcCall(usr, object, delegate, calling_arguments)
         return WrapAdminProcCall(object, delegate, calling_arguments)
C#:  if (Dm.truthy(Dm.bitand(datum_flags, DF_VAR_EDITED)))
     {
         Dm.NotTranslated("code/datums/callback.dm:113", "Invoke: the DF_VAR_EDITED admin proc-call branch");
         return null;
     }
```

**1.5 Where things go.**

| DM | C# location |
|---|---|
| a type `/a/b/c` with its vars and procs | `public partial class a_b_c : a_b` in the mirror file of each `.dm` that defines part of it (section 2.3) |
| a global proc `/proc/x` | `public static partial class GlobalProcs` (namespace `Anomaly.Core.Tg`) in the mirror file of its `.dm` file (section 7.8) |
| a proc on `/world` (`/world/proc/push_usr`, `__HELPERS/mobs.dm:709`) | a C# extension method of the shim's facade `DmWorld` (its first parameter `this DmWorld world`), `public static` in `GlobalProcs` in the mirror file of its `.dm` file; it is not virtual (tg never subtypes `/world`). Calls read as in DM, `world.push_usr(M, this)`, and the binder reaches it by name (`Dm.Call(world, "push_usr", ...)`, `call(world, name)`), passing `world` first (`docs/ai/specs/object-model.md` section 9) |
| a function-like tg macro that section 11.1 does not expand inline (whether or not it touches tg types: `QDELETED`, `CEILING`, `LAZYADD`) | a static member of `GlobalProcs` in the mirror file of the `.dm` that `#define`s it (section 11); a tg macro that expands to a call on `src` or an explicit receiver (`AddComponent`, 11.1) is an instance method of the receiver's class there |
| a file-local `#define` (one outside `code/__DEFINES`, `#undef`ed at the end of its file) | a `private const` (function-like: a private member) of the class in the file's mirror that uses it (section 11.4) |
| a BYOND builtin proc that needs no tg type (`prob`, `round`, `length`, `findtext`, `istype` with a `Type`...) | `Dm` in `Anomaly.Core.DmShim` (wave 1; file owners in `docs/ai/specs/shim-ownership.md`) |
| a BYOND builtin proc that needs atoms, turfs or the map (`get_step`, `get_dist`, `get_dir`, `range`, `view`, `orange`, `oview`, `hearers`, `viewers`, `block`, `locate(x, y, z)`, `step`, `walk*`, and the type tests `ismob`, `isturf`, `isarea`, `ismovable`, `isloc`, which tg does not `#define`) | `public static partial class Builtins` in `Anomaly.Core.Tg` (files `src/Core/Tg/Builtin/*.builtin.cs`), called bare: `get_step(this, dir)` |
| every `#define` constant | the generated `public static partial class Defines` in `Anomaly.Core.Tg` (`tools/Anomaly.Tools.Defines`) |
| global and static state (`GLOB.*`, `var/static`, proc-level `var/static`, `var/global`, `SS*`, `Master`) | per-world containers (section 10.6, `docs/ai/specs/object-model.md`) |

**1.6 The shim never defines a name that tg defines.** `Dm` and `Builtins`, which translated files import with `using static`, define only BYOND builtins and port helpers, never a name that tg defines as a `#define` (anywhere in the tg build) or as a global proc: `INFINITY`, `PI`, `NONE` come from `Defines`, tg's `is*` type-test macros (`isobj`, `isdatum`, `isatom`, `isimage`, `isliving`, `iscarbon`, ...) from `GlobalProcs`, never from `Dm`; the `is*` tests that tg leaves to BYOND (`ismob`, `isturf`, `isarea`, `ismovable`, `isloc`; `is_helpers.dm:28` only comments `isturf` out) are `Builtins`, and `isnum`, `istext`, `islist`, `ispath`, `isfile`, `isicon` are `Dm` (`isnull(x)` is `x == null`, 3.6); `usr` is the `GlobalProcs` accessor typed `mob` (10.8), so `Dm` does not define it either; two `using static` classes with the same member name make every call ambiguous (CS0229, CS0121). A reflection test owned by the defines package fails when a public member name of `Dm` or `Builtins` equals one of `Defines` or `GlobalProcs`. BYOND builtins that tg redefines are tg's: tg's `ASSERT(condition, message...)` (`__DEFINES/assert.dm:4`) replaces BYOND's and is expanded inline at each use (11.1, 9.5), so no class defines `ASSERT`; tg's `url_encode` and `url_decode` (`__DEFINES/rust_g_overrides.dm:2-3`, rust-g wrappers) and `in_range` (`__DEFINES/is_helpers.dm:3`) are `GlobalProcs` members, and so is tg's global proc `get` (`__HELPERS/atoms.dm:312`), which is why the shim's helpers for `A in X to Y` and `a:b` are named `Dm.in_to` and `Dm.get_var`. `DefineClashTests` (`tests/Anomaly.Core.Tests/Contracts/`) scans the tg build (`tgstation.dme` and every `.dm` under `code/`, `interface/` and `_maps/`) for `#define`s, global procs and the names of the `GlobalProcs` accessors (`GLOBAL_REAL`, `GLOBAL_REAL_VAR`, `SUBSYSTEM_DEF` and its variants) and fails on a clash with `Dm`, `Builtins` or a shim or Kernel type.

**1.7 Conditional compilation.** `#if`/`#ifdef`/`#ifndef` blocks in procs and type bodies are resolved with tg's production configuration: `code/_compile_options.dm` as shipped (`UNIT_TESTS`, `TESTING`, `REFERENCE_TRACKING`, `TIMER_DEBUG` and the other debug switches undefined), the `.dme`'s own defines, `DM_VERSION` 516 and `DM_BUILD` 1685 (tg's `dependencies.sh`), `CBT` defined (tg's build passes it to every compile, `tools/build/build.ts:229`; it only selects the compile-time side of `MAP_SWITCH` and `WHEN_COMPILE`, `__DEFINES/map_switch.dm`), plus `DISABLE_DREAMLUAU` defined (as tg does for OpenDream, `_compile_options.dm:133`: the Lua admin scripting is not ported). `tools/coloring` and the defines converter (`tools/Anomaly.Tools.Defines`) use exactly this configuration (`docs/ai/specs/colored-procs.md`, Method 2; `docs/ai/specs/defines-converter.md`). Only the active branch is translated; the inactive one leaves a single comment, `// tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)`. So code under `#if defined(UNIT_TESTS)` or `#ifdef UNIT_TESTS` is never translated (the port builds without `UNIT_TESTS`; ported tg unit tests are test code, section 15), and `PERFORM_ALL_TESTS(x)` is written as its production expansion `FALSE` (`__DEFINES/unit_tests.dm:12`, `UNLINT(FALSE)`), whatever `x` is: `if(PERFORM_ALL_TESTS(dcs_check_list_arguments) && islist(key))` (`controllers/subsystem/dcs.dm:82`) is `if (Dm.truthy(FALSE) && islist(key))`. Calls into native libraries (rust-g `rustg_*`, TGS, `call_ext`) are not translated (section 13).

**1.8 Layout.** Allman braces and four-space indentation (`.editorconfig`). One DM statement per C# line, in DM order. A control-flow body (`if`, `else`, `for`, `foreach`, `while`, `do`) that is exactly one simple statement (an expression statement, `return`, `break`, `continue`, `throw`) has no braces; any other body is a braced block. `else if` chains stay chains.

```
DM:  if(!user)
         return
     for(var/obj/item/I in user)
         if(I.force > 10)
             to_chat(user, "Heavy!")
             break
C#:  if (!Dm.truthy(user))
         return;
     foreach (obj_item I in Dm.each<obj_item>(user))
     {
         if (I.force > 10)
         {
             to_chat(user, I18n.T("Heavy!"));
             break;
         }
     }
```

**1.9 Partial, skeleton and interleaved mirror files.**

- **Partial mirror.** A package may translate only some members of a `.dm` file that another package owns (shared macro files such as `__DEFINES/_helpers.dm`, `__DEFINES/is_helpers.dm`, `__DEFINES/maths.dm`). Its mirror file then starts, right after the header block of 1.2, with the line `// PARTIAL: only <members> translated` (a short reason may follow), and holds only those members; the package that translates the file later completes it and removes the line. The lead pre-assigns each shared macro file to one package per wave, so that two packages never create the same mirror file.
- **Skeleton mirror.** A file that only declares what other translated code needs (a class with `[DmPath]` and the members others name, bodies `Dm.NotTranslated`) starts after the header block with `// SKELETON: completed <when, by which package>`. A skeleton never holds translated tg logic.
- **Interleaved types.** A `.dm` file that defines procs of two types in alternation (`datums/components/_component.dm`: `/datum/component` procs at :12-237, `/datum` procs at :255-426, `/datum/component` at :435, `/datum` at :452-480, `/datum/component` at :498-502) is mirrored as alternating `partial class` blocks of the two classes, in tg's order, one block per run of consecutive definitions of one type; every block repeats the base class (2.3).

---

## 2. Names

**2.1 Verbatim.** Vars, procs, params, locals, constants and macros keep their DM spelling and case: `gc_destroyed`, `RegisterSignal`, `COMSIG_MOVABLE_MOVED`, `get_turf`, `forceMove`, `seconds_per_tick`. No PascalCase, no camelCase.

**2.2 C# keywords** get `@`: every C# keyword used as a DM name, for example `@event`, `@params`, `@base`, `@in`, `@out`, `@ref`, `@lock`, `@checked`, `@object` (`/datum/callback/var/object`), `@delegate`, `@override` (`RegisterSignal`'s parameter), `@string`, `@operator`, `@default`, `@new`, `@fixed`, `@class`, `@namespace`. Of the contextual keywords, `await` is a DM name in tg (`/datum/timed_action/proc/await`, timed_actions.dm:103) and is always written `@await`, at the declaration and at every call; the others (`value`, `get`, `set`, `async`, `record`, ...) are written plainly. Reflection reports the name without `@`, so the DM name is preserved for dynamic calls.

**2.3 Type names.** A DM type path becomes one class whose name is the path without the leading `/` and with every `/` replaced by `_`, segments verbatim: `/datum` -> `datum`, `/atom/movable` -> `atom_movable`, `/datum/component/storage` -> `datum_component_storage`. C# inheritance equals DM path inheritance (`/obj` -> `obj : atom_movable`, `/turf` -> `turf : atom`, explicit `parent_type` respected). Every class carries `[DmPath("/atom/movable")]` exactly once, on the part in the mirror of the earliest file (in `tgstation.dme` include order) that declares the type or one of its vars, procs or var overrides; every part repeats the base class. When that earliest file is not translated yet, a skeleton mirror of it (1.9) is created that holds the class declaration and `[DmPath]`, so the attribute's place is fixed by the `.dme` alone and never moves when more files are translated. Declarations that only a macro expansion produces do not count (the `GLOBAL_VAR*`/`GLOBAL_LIST*` macros declare `/datum/controller/global_vars` vars in hundreds of files; that type's `[DmPath]` stays on `controllers/globals.cs`, the file that declares it in its own text). A BYOND builtin type (`/datum`, `/atom`, `/atom/movable`, `/obj`, `/mob`, `/turf`, `/area`, `/client`, ...: BYOND declares it before the `.dme`) carries `[DmPath]` on its engine part `src/Core/Tg/Builtin/<class>.builtin.cs` (1.1), and its mirror files carry none. Classes are never C# `abstract` or `sealed` (DM can instantiate any type; `abstract_type` is an ordinary tg var).

*Collisions.* The mapping is not injective: two paths that differ only in `/` versus `_` map to one name. A scan of the snapshot finds two such pairs (Appendix B.1). Rule: the colliding path with **fewer** segments (the one with `_` inside a segment) gets the suffix `__2`; the other keeps the plain name. Both carry their `[DmPath]`, which is what dynamic code uses.

```
/obj/projectile/energy/tesla/cannon  -> obj_projectile_energy_tesla_cannon
/obj/projectile/energy/tesla_cannon  -> obj_projectile_energy_tesla_cannon__2
```

**2.4 Member name clashes.** C# forbids a field and a method with the same name in one class and warns when a member hides an inherited one; DM allows a var and a proc with the same name. When tg declares a var and a proc with the same name on the same type or along one ancestor line (71 names at the snapshot, Appendix B.2), the **proc** is renamed `<name>__proc` in the root declaration and in every override; the var keeps its name. Calls, `..()`, `PROC_REF` and signal registrations use the C# name; the dynamic binder maps `x__proc` back to the DM name `x` by stripping the suffix.

```
DM:  /atom/var/can_atmos_pass = ATMOS_PASS_YES      (LINDA_system.dm:3)
     /atom/proc/can_atmos_pass(turf/target_turf, vertical = FALSE)   (LINDA_system.dm:5)
C#:  public float can_atmos_pass = ATMOS_PASS_YES;
     public virtual object can_atmos_pass__proc(turf target_turf = null, float? vertical = null)
```

**2.5 Port suffixes.** Names the port introduces are DM names plus a suffix that starts with `__`: `__proc` (2.4), `__2` (2.3), `__d0` (an earlier duplicate definition of a proc in the same type; a proc defined more than twice numbers its earlier definitions `__d0`, `__d1`, ... in compile order, `/world/_initialize_log_files` in `_globalvars/logging.dm`, `docs/ai/specs/move-chain.md` section 9, item 1, and BYOND's builtin definition of a proc that tg defines on the builtin type itself, 7.5), `__end` / `__step` (section 6.6), `<label>__break` / `<label>__continue` (the labels of a labelled loop, section 6.8), `__it` (a loop var declared before its `for`, sections 6.5 and 6.6), `__s` (spawn copies, section 8), `__body` (the body of a `set waitfor = FALSE` wrapper, `docs/ai/specs/waiting-model.md` 5.4), `usr__caller` (10.8), `GLOB__var` (GLOB as a value, 10.6), `<proc>__<var>` (a proc-level static's accessor, 10.6), `<n>__1`, `<n>__2` (renamed shadowing locals and pattern temps), `<var>__gap` (a local that stands in, read as null, for a global, a subsystem or a builtin var the port does not declare yet, with a `GAP` or `SHIM-GAP` comment at the site; a var of a builtin object keeps its receiver: `SSstickyban__gap` for the undeclared `SSstickyban`, `world_address__gap` for `world.address`, both in `client/New`). The params array of a variadic slot is always named `__extra` (section 7.2). Before introducing one, check that the resulting identifier does not occur in tg (`grep -rw`); tg itself uses `__` as a prefix in a few names (`__avd_`, `___TraitAdd`), never these suffixes. A proc the port adds that has no tg counterpart but carries tg lines (the locomotion bridge's `capsule_move_gate`, `capsule_move_tail` and `capsule_mob_bump`, D105; `help_shake_act`'s shared tail `hug`, `carbon_defense.cs`) has a DM-style snake_case name without a suffix, is `private` when only its own class calls it, and starts with a comment that names why it exists; the tg lines inside keep their `// tg:` pointers (M2 integration).

**2.6 Locals.** C# forbids a local that shadows another local or a param of the enclosing scopes (CS0136); DM allows it. The inner declaration is renamed `<name>__1` (`__2` for the next one in the same proc) for its whole scope. Temps that the translation introduces are named `<role>__<n>`: `sw__<n>` (switch subjects, 6.7), `or__<n>` (pattern temps of `||` and `||=`, 4.5), `and__<n>` (of `&&` and `&&=`), `t__<n>` (an operand evaluated once, 4.12, including a macro argument that the expansion evaluates more than once). Each role has its own counter, numbered per proc from 1 in order of appearance in the C# text (`or__1`, `or__2`, `and__1`). A local that an inline macro body declares (`var/__move_args` in `RESOLVE_ACTIVE_MOVEMENT`) keeps its DM name; it is renamed by the rule above only when it clashes.

```
DM:  var/x = 1
     if(cond)
         var/x = 2
         use(x)
C#:  float x = 1;
     if (Dm.truthy(cond))
     {
         float x__1 = 2;
         use(x__1);
     }
```

---

## 3. Values and their C# types

### 3.1 The DM value model

DM has one dynamic value type: number (IEEE binary32, 24-bit exact integers), text, null, object reference, list, typepath, resource. The port gives each DM var, param, local and proc return value **one static C# type**, chosen by the deterministic procedure below. The available C# types are:

| C# type | holds | used for |
|---|---|---|
| `float` | a DM number that is never null | numeric vars with a numeric default (unless null-used, rule V4), numeric locals |
| `float?` | a DM number or null | null-used numeric vars, numeric params with a non-null default (rule P3) |
| `string` | DM text or null | text vars and params with a text default, text locals |
| a translated class (`atom`, `obj_item`...) | an object of that DM type or null | typed vars, params and locals |
| `DmList` | a DM list or null | `var/list/...` and list-typed locals |
| `Type` | a DM typepath or null | vars, params and locals that hold typepaths (rule V3) |
| `object` | anything | untyped vars without a usable default, untyped params, mixed locals, proc return values (section 7.4) |

**There is no C# `bool` and no C# `int` for DM values.** `TRUE` and `FALSE` are DM numbers (the shim's `const float TRUE = 1f, FALSE = 0f`). A C# `bool` exists only as the result of a condition (comparison, `Dm.truthy`, `is`), and becomes a DM value only through `Dm.num(b)` (1f or 0f). The generated `Defines` emit some constants as `const int` (integer literals and bit flags, `docs/ai/specs/m0-reading/defines.md` 5.2); they convert to `float` implicitly where used, and the shim treats a boxed `int` exactly like the same `float` (below).

**Number literals.** A DM integer literal is written as is (`5`, `-1`, `0x1F`); C# converts it to `float` wherever a `float` is expected. A DM literal with a fractional part or an exponent gets the `f` suffix (`0.1f`, `2.5f`, `1e31f`); C# `double` never appears. Integer literals of 2^24 and above (`16777216`, tg's `SHORT_REAL_LIMIT`) keep their exact value as written. In an `object` context (a list element, an `object` var, argument or return value: `final_list[key] = 1`, `return 1`) an integer literal also stays as written and becomes a boxed `int`, never `1f`: the shim treats a boxed integral (`int`, `long`) exactly like the `float` of the same value everywhere (`Dm.eq`, `in`, the `DmList` find, remove and number keys, text, `json_encode`, the state hash, `isnum`, truthiness), so translated code never needs to tell them apart. Tests compare DM values with `Dm.eq` or `Dm.num`, never with `object.Equals` or `Assert.Equal` on boxed values (`object.Equals(0, 0f)` is false; section 15).

### 3.2 Type-level vars

First matching rule wins. "The null-use grep" is, with `NAME` the var name:

```
grep -rnE "isnull\(([A-Za-z_][A-Za-z0-9_.?:\[\]]*\.)?NAME\)|(^|[^A-Za-z0-9_])NAME\s*(==|!=)\s*null\b|\bnull\s*(==|!=)\s*([A-Za-z0-9_?.]*\.)?NAME\b|(^|[^A-Za-z0-9_])NAME\s*=\s*null\b" ../tgstation/code --include='*.dm'
```

It matches by name over the whole tree, so it over-approximates (a same-named var on an unrelated type counts too). Over-approximation is safe: it only turns a `float` into a `float?`. The type-tree converter will later replace this grep with an exact per-var analysis that keeps the same rule.

| # | DM declaration | C# type |
|---|---|---|
| V1 | `var/list/x ...`, `var/x[...]`, `var/list/<type>/x` | `DmList` |
| V2 | `var/<typepath>/x ...` (declared type) | the class of that path, except V3 |
| V3 | the var holds typepaths: its declared default is a typepath literal (`= /obj/item/x`), `type`, `parent_type`, or tg assigns a typepath literal to it anywhere (`grep -rnE "\bNAME\s*=\s*/[a-z]" ../tgstation/code --include='*.dm' \| grep -vE "\bNAME\s*=\s*(/[A-Za-z_0-9]+)*/(proc\|verb)/"`), or typepaths read from a list (below); a proc or verb path is not a typepath (below) | `Type` |
| V4 | untyped, default is a numeric constant (number literal, `TRUE`, `FALSE`, a numeric `#define`, a constant expression of those, a time-unit expression like `2 SECONDS`) | `float`; `float?` when the null-use grep matches; `object` when the var later holds a stoppable timer's id, a text (the converter's TimerIdVars table, silicons SI8) |
| V5 | untyped, default is text (literal or constant text expression) or a proc reference (`GLOBAL_PROC_REF(x)`, `PROC_REF(x)`, `/proc/x`: text by 7.7) | `string` |
| V6 | untyped, default is `list(...)` / `new /list` | `DmList` |
| V7 | anything else: untyped with no default, `= null`, an object-creating default of a non-list type (`= new /datum/x`), or a mixed default | `object` |

Examples (V-rule in brackets):

```
/obj/item/bedsheet/var/list/dream_messages = list("white")   (bedsheet_bin.dm:31)        -> public DmList dream_messages;          [V1; the list itself: section 10.2]
/datum/var/gc_destroyed                                                                   -> public object gc_destroyed;            [V7]
/datum/effect_system/basic/var/amount = 3   (effect_system.dm:51; isnull(amount) at :73)  -> public float? amount = 3;              [V4, null-used]
/atom/movable/var/moving_diagonally = 0     (atoms_movable.dm:55; never null-used)        -> public float moving_diagonally = 0;    [V4]
/datum/component/var/dupe_mode = COMPONENT_DUPE_HIGHLANDER   (_component.dm:18)           -> public float dupe_mode = COMPONENT_DUPE_HIGHLANDER;  [V4]
var/supposed_type = /turf/closed/mineral/volcanic/lava_land_surface   (minerals.dm:957)  -> public Type supposed_type = typeof(turf_closed_mineral_volcanic_lava_land_surface);  [V3]
/obj/item/var/hitsound                      (items.dm:78; no default)                     -> public object hitsound;                [V7]
/datum/sort_instance/var/cmp = GLOBAL_PROC_REF(cmp_numeric_asc)   (sorts/sort_instance.dm:18)  -> public string cmp = "/proc/" + nameof(GlobalProcs.cmp_numeric_asc);  [V5, a proc reference]
```

**Proc references are text, not typepaths.** A proc or verb path (`/proc/cmp_text_asc`, `/verb/x`, `/datum/foo/proc/bar`) and the macros `PROC_REF`, `GLOBAL_PROC_REF`, `VERB_REF`, `TYPE_PROC_REF` and `TYPE_VERB_REF` are text in the port (7.7), although `GLOBAL_PROC_REF(x)` expands to the path `/proc/x` (`__byond_version_compat.dm:35`). V3, P1 and the V3 grep never count them as typepaths, and a var, parameter or local whose default or stored values are proc references is `string` (V5, P4, L3). `tools/coloring/slots.py` applies this to slot parameters (`slots.tsv` notes them `string (proc reference default, 7.7)`: the `cmp` of `sortTim`, `sortMerge`, `sortInsert`, `sort_list` and `assert_sorted`).

**Typepaths read from a list.** A typed var or local (V2, L1) that holds typepaths taken from a list is `Type` (V3), not its declared class, whose `as` downcast (3.5) would turn every path into null; tg reads a typepath out of an argument list with a typed local on purpose (`var/datum/element/eletype = arguments[1]`, `controllers/subsystem/dcs.dm:57`, where the first argument of `AddElement` is the element's type: `Type eletype = arguments[1] as Type;`, and `initial(eletype.element_flags)` is `Dm.initial(eletype, (datum_element p) => p.element_flags)`, 10.3). `T::parent_type` and `initial(T.parent_type)` with `T` a `Type` value are `Dm.parent_type(T)` (10.3, 10.7).

**Typed vars that hold wider values.** A typed var (V2) whose stored values are wider than its declared type is `object`, like the typed locals and parameters of 3.4: when its default is of another kind (`/datum/callback/var/datum/object = GLOBAL_PROC`, `datums/callback.dm:46`, where `GLOBAL_PROC` is a text constant: `public object @object = GLOBAL_PROC;`, the "-" cell of 3.5) or when tg stores in it values that are neither of the declared type nor of a subtype (and not of another kind that 3.4 tells apart with a type test). Each member access then casts to the class that **declares** the member, as 11.1 does for macro parameters: `((datum)@object).type`.

A subtype that only overrides a default (`force = 10` under `/obj/item/crowbar`) declares nothing; it assigns in its constructor (section 10.2).

Note on builtin vars: BYOND builtin vars of `/datum` and `/atom` (`name`, `desc`, `icon`, `icon_state`, `loc`, `x`, `y`, `z`, `dir`, `density`, `opacity`, `layer`, `contents`...) are declared once by the engine part `src/Core/Tg/Builtin/*.builtin.cs` with fixed types listed in `docs/ai/specs/object-model.md`; translators never redeclare them. `name` and `desc` are `string`.

A subtype that overrides a var's default never changes its C# type; the type is fixed by the declaration (the type-level `var/` line).

### 3.3 Proc parameters

The **slot** (section 7.1) fixes every parameter type once, at the root declaration; overrides reuse it. Every C# parameter gets the C# default `= null` so callers can omit it (DM allows omitting any argument).

| # | DM parameter | C# parameter |
|---|---|---|
| P1 | typed, `obj/item/I`, `list/L` | the class / `DmList`, `= null`; `Type` (V3's test applied to the param) when some definition of the slot gives it a typepath default (never a proc reference, 3.2) or uses it as a type in `new P` or `typesof(P)`. `ispath(P)` is a type test of 3.4, not a use as a type (tg tests with it a parameter that holds a path or an instance). `initial(P.v)` and `P::v` read an instance as well as a type (10.3), and an `ispath(P)` test that 3.4 gives no verdict for leaves the value a typepath on one side: these leave the parameter undecided. `slots.tsv` lists such a parameter in its `undecided` column and keeps the declared type; the root's translator decides it by reading the listed sites and records a change in Appendix A (7.1) |
| P2 | untyped, no default (an explicit `= null` counts as no default) | `object x = null` |
| P3 | untyped, numeric constant default (`force = FALSE`, `vol = 50`, `falloff = SOUND_FALLOFF_EXPONENT`) | `float? x = null` plus the default prologue (section 7.2) |
| P4 | untyped, text default, or a proc reference default (`cmp = GLOBAL_PROC_REF(cmp_numeric_asc)`, `sorts/helpers.dm:56`; `cmp=/proc/cmp_text_asc`, `_lists.dm:733`; 3.2) | `string x = null` plus the prologue |
| P5 | typed or untyped with a non-constant default (`view = world.view`, `severity_max = severity_min`, `scheduler = SSmachines`, `list/blacklist = list()`) | the P1/P2 type, `= null`, plus the prologue |
| P6 | `...` | `params object[] __extra` (section 7.2) |
| P7 | `as num`, `as text` and other input hints | ignored; decide by P1-P5 |

An untyped parameter whose default is a typepath (`spawn_type = /obj/item/x`) is `Type x = null` plus the prologue (V3 applied to the param). These rules are what `tools/coloring/slots.py` computes for `slots.tsv` (`docs/ai/specs/colored-procs.md`, "Slot registry"); where the two differ, report it to the lead. Before P1's `Type` test, a typed parameter is `object` when an override gives it a default of another kind (3.5) or when the 3.4 analysis of typed parameters (below), run over every definition of the slot, retypes it.

**A narrower type in an override or a callee (M2 integration).** DM does not check parameter types. When an override declares its parameter narrower than the slot's root (`/mob/living/carbon/human/attack_hand(mob/living/carbon/human/user)` under `/atom/attack_hand(mob/user)`), the C# parameter keeps the root's type and the body narrows it with a cast where tg's code relies on the narrower type, or with `as` where a value of another type can arrive (`var/mob/living/carbon/human/H = user as mob_living_carbon_human`). The same holds for a typed parameter that tg's own callers fill with a wider type (the lungs' `handle_suffocation(mob/living/carbon/human/suffocator)`, called with any carbon): `as`, which gives null for the other types, as DM's typed var would hold the value but fail on the human-only member. A slot parameter that tg's callers fill with another kind of value altogether is `object` by a "-" cell of 3.5, a per-slot decision of Appendix A (`log_combat`'s `atom/object`, given text at `living_defense.dm:855`).

### 3.4 Locals

| # | DM local | C# local |
|---|---|---|
| L1 | `var/<typepath>/x`, `var/list/x` | the class / `DmList` / `Type` (V3 test within the proc, including typepaths read from a list, 3.2) |
| L2 | untyped, and every value assigned to it in the proc (initializer included) has C# type `float` (or `int`), and it is never null-tested or assigned null | `float` |
| L3 | untyped, every assigned value is `string` | `string` |
| L4 | untyped, otherwise | `object` |

Never use C# `var` for a DM local; write the type. (`var` is allowed only for temps the rules introduce and for C# pattern variables.) A DM local declared without a value starts as DM null: `object x = null;`, `float x = 0;` (an L2 local is never observed as null), `string x = null;`.

```
DM:  var/total = 0                  C#: float total = 0;            (L2: only numbers are assigned to it)
DM:  var/key = "cooldown_[index]"  C#: string key = Dm.text("cooldown_[]", index);   (L3)
DM:  var/thing = L[1]               C#: object thing = L[1];         (L4)
DM:  var/obj/item/I = thing         C#: obj_item I = thing as obj_item;   (L1 with the downcast of 3.5)
```

**Typed locals and parameters that tg fills with other kinds.** A DM declared type is only a compile-time hint, and tg sometimes stores other kinds in a typed var on purpose and then tells them apart: `RegisterSignal` keeps either one datum or a list of datums in `var/list/looked_up` (`datums/signals.dm:46-53`), and `/atom/proc/build_appearance_list(list/build_overlays)` wraps a non-list argument with `build_overlays = list(build_overlays)` (`controllers/subsystem/overlays.dm:42`). The `as` downcast of 3.5 would turn the datum into null there. Rule: a typed local (L1) or typed parameter (P1; for a slot parameter, checked over every definition of the slot, 7.1) is `object` instead when the proc type-tests it (`istype`, `islist`, `isnum`, `istext`, `ispath`, `astype`, a BYOND or tg `is*` test, and `length`, below) and reads it again on a path where that test showed it is **not** of its declared type, other than in another type test or a null test. Each use that needs the declared type then casts at the use, `((DmList)looked_up).len`, as for narrowed parameters (7.2). The frequent `var/obj/item/I = O` / `if(!istype(I)) return` idiom reads nothing on the failing path and keeps the typed form.

- **`length()` is a type test**, read like `islist`: tg tells one item from a list of items with `length`, which is 0 for a datum (`datums/datum.dm:177-183`: `var/list/comps = lookup[sig]`, `if(length(comps))` the listeners of a list, `else` the single listener `comps` itself). A local tested with `length()` and used as a non-list on its failing path is `object`.
- **Wider stored values.** A typed local is also `object` when tg stores in it values wider than its declared type, so that its declared class is only one of several possibilities and the `as` downcast or `each_cast` of 3.5 and 6.5 would null or reject the others: the listeners in `_listen_lookup` are datums of any type, but `datums/datum.dm:179` and `:182` declare them `var/datum/component/comp`; `REMOVE_TRAITS_NOT_IN` stores one text source in `var/list/_S = sources` (`__DEFINES/traits/_traits.dm:74`). A typed loop var of `for(... as anything in ...)` follows the same rule and iterates with `Dm.each`. Each member access casts to the class that **declares** the member (3.2): `((datum)comp).UnregisterSignal(this, sig)`.
- **Text rendering is not a read.** A read that only renders the local into text (an embedded expression of `Dm.text` or `I18n.T`, the message of `CRASH`, `ASSERT`, `stack_trace` or a `log_*` call) never forces retyping a local: text accepts every kind. (For slot parameters, `slots.tsv` lists such a read as undecided, 3.3.)

```
DM:  var/list/looked_up = lookup[signal_type]          (datums/signals.dm:46)
     if(isnull(looked_up)) // Nothing has registered here yet
         lookup[signal_type] = src
     else if(!islist(looked_up)) // One other thing registered here
         lookup[signal_type] = list(looked_up, src)
     else // Many other things have registered here
         looked_up += src
C#:  object looked_up = lookup[signal_type];
     if (looked_up == null) // Nothing has registered here yet
         lookup[signal_type] = this;
     else if (!islist(looked_up)) // One other thing registered here
         lookup[signal_type] = Dm.list(looked_up, this);
     else // Many other things have registered here
         Dm.append(ref looked_up, this);
```

```
DM:  var/list/comps = lookup[sig]                     (datums/datum.dm:177)
     if(length(comps))
         for(var/datum/component/comp as anything in comps)
             comp.UnregisterSignal(src, sig)
     else
         var/datum/component/comp = comps
         comp.UnregisterSignal(src, sig)
C#:  object comps = lookup[sig];
     if (Dm.truthy(length(comps)))
     {
         foreach (object comp in Dm.each(comps))
             ((datum)comp).UnregisterSignal(this, sig);
     }
     else
     {
         object comp = comps;
         ((datum)comp).UnregisterSignal(this, sig);
     }
```

### 3.5 Conversions

When a value of one C# type flows into a target of another (assignment, argument, return, list element read), the translator writes exactly this:

| target \ source | `float` / `int` | `float?` | `object` | C# `bool` | `string` | class / `DmList` / `Type` |
|---|---|---|---|---|---|---|
| `float` | as is | `Dm.num(x)` | `Dm.num(x)` | `Dm.num(b)` | - | - |
| `float?` | as is | as is | `Dm.num_or_null(x)` | `Dm.num(b)` | - | - |
| `object` | as is (boxed) | as is | as is | `Dm.num(b)` | as is | as is |
| `string` | - | - | `(string)x` | - | as is | - |
| class `T` | - | - | `x as T` | - | - | as is for a subclass, `x as T` for a base class (DM implicit downcast), - for an unrelated class, `DmList` or `Type` (C# rejects `as` there, CS0039) |
| `DmList` / `Type` | - | - | `x as DmList` / `x as Type` | - | - | as is for the same type, - otherwise |

A "-" cell means DM code stores a value of that kind in a var of the other kind, which the typing rules of 3.2-3.4 did not foresee: the var is retyped `object` at its declaration (for a slot parameter, a per-slot decision recorded in Appendix A and reported to the lead, 7.1), never patched at the use.

`Dm.num` turns null into 0, keeps numbers, and raises a DM runtime error ("type mismatch") for anything else, as DM arithmetic does. `Dm.num_or_null` keeps null. (Storing an `object` that holds text into a `float` var therefore raises where DM would store the text; when a test or a review finds such a site, the var is retyped `object`.) The implicit downcast uses `as` because tg's idiom `var/obj/item/I = O` followed by `if(istype(I))` must see the object as "not an item" rather than crash at the assignment (2,170 single-argument `istype` sites); member access on a wrongly typed value then fails with a null reference, which is a runtime error like DM's.

- **No `as` before a `?.` where DM raises.** DM's `a?.b` skips a null `a` but raises a runtime error ("undefined variable") for a non-null `a` of the wrong type; after an `as` downcast the C# `?.` would read a silent null instead. A downcast whose local is never type-tested in the proc and is read with `?.` is therefore the cast `(T)x`, which lets null through and raises an `InvalidCastException` (a DM runtime error, 9.1) for a wrong type: `var/datum/sprite_accessory/value = input[key]` / `if(value?.locked)` (`assoc_to_keys_features`, `__HELPERS/_lists.dm:988-989`) is `datum_sprite_accessory value = (datum_sprite_accessory)input[key];` / `if (Dm.truthy(value?.locked))`. A local that the proc type-tests keeps the `as` form.
- **Text builtins take the object.** The text builtins that DM lets take any value (`sorttext`, `sorttextEx`, `cmptext`, `cmptextEx` and the other text procs of Appendix C.1 that tg calls with possibly non-text values) declare `object` parameters, so an `object` argument is passed as is, never through `(string)x`, which would throw for a mob or a typepath that DM renders or compares as text (tg sorts mobs and typepaths with `cmp_text_asc`, `__HELPERS/cmp.dm`). What such a builtin does with a value that is not text is the shim's (`docs/ai/specs/dm-conformance.md`).

### 3.6 null

null is an ordinary value. In arithmetic it is 0 (`Dm.num`), in text it is `""` (`Dm.str`), in conditions it is false. Every reference may be null; the project has nullable reference annotations off. `isnull(x)` is `x == null`; `!isnull(x)` is `x != null`. A `float` (non-nullable) expression never meets `isnull`; if it does, the var should have been `float?` (the null-use grep catches all of tg's `isnull(name)` forms).

```
DM:  if(isnull(amount))             C#: if (amount == null)           (amount is float?, V4)
DM:  target = null                  C#: target = null;
DM:  . = amount + 1                 C#: dot = Dm.num(amount) + 1;     (null counts as 0)
```

### 3.7 Text, typepaths, references

- **Text** is `string`. DM text is immutable like C# text; concatenation creates a new string.
- **Typepaths** are `System.Type` objects of the generated classes (`typeof(obj_item)`); `/list` is `typeof(DmList)`. Text form and path lookup go through the type registry `DmTypes` (`docs/ai/specs/object-model.md`). A typepath literal in code is `typeof(<class>)`.
- **Object references** are direct C# references. `qdel`ed objects stay in memory while referenced, exactly like DM objects with references. Identity for replication and `REF()` is `dm_id` (`docs/ai/specs/object-model.md`).
- **Resources.** A resource literal `'path'` (`'icons/turf/floors.dmi'`, `'sound/x.ogg'`) is `Dm.resource("path")`, an immutable `DmResource` value: `isfile` is true for it, `isicon` for an image file, it renders as its path in text, two resources of one path are equal (`==`, list keys) and a resource never equals the text of its path. A var, param or local that holds a resource is `object` (3.2). `Dm.resource` never touches the World, so it may stand in a field initializer or a constructor (10.2). A `#define` whose body is a resource literal is not emitted by the defines converter; each use expands inline to `Dm.resource("path")` (`DEFAULT_HUDS_DMI`, `__DEFINES/atom_hud.dm:75`).
- **BYOND builtin value types** (`/list`, `/regex`, `/matrix`, `/icon`, `/image`, `/mutable_appearance`, `/sound`, `/generator`, `/exception`) are shim or builtin classes with DM-named members; they are created with `Dm.@new` like any type but get no `dm_id`.

### 3.8 Fields, not properties

DM vars are public C# **fields**: tg macros assign the caller's var and need `ref` (`QDEL_NULL(ref x)`, `LAZYADD(ref L, x)`). `VAR_PRIVATE` / `VAR_PROTECTED` / `VAR_FINAL` keep the field `public` and add `[DmPrivate]` / `[DmProtected]` / `[DmFinal]` markers. The only properties are BYOND builtin vars with engine behaviour (`tag`, `loc`, `x`, `y`, `z`, `contents`, `icon_state`...), declared in the Builtin parts and listed in `docs/ai/specs/object-model.md`.

### 3.9 Culture

Every number-to-text and text-to-number conversion in `src/Core` goes through the shim (`Dm.str`, `Dm.text`, `num2text`, `text2num`), which uses `CultureInfo.InvariantCulture` and DM's format. Translated code never calls `ToString()`, `string.Format`, `$"{x}"`, `float.Parse` or `Convert` on numbers. Reason: the core also runs inside the Godot client, where the process culture applies (`InvariantGlobalization` only covers executables built here); on a `pl-PL` machine `0.5f.ToString()` is `"0,5"`. A shim test sets `CultureInfo.CurrentCulture = new("pl-PL")` and checks `Dm.str(0.5f) == "0.5"` and `text2num("0.5") == 0.5f`.

---

## 4. Expressions and operators

### 4.1 Conditions and truthiness

A DM condition (`if`, `while`, the test of a `for`, the condition of `?:`, an operand of `!`, `&&`, `||` in a condition) becomes a C# `bool`:

- a comparison, `Dm.eq`, `Dm.@in`, `x is T`, `x == null`, and the boolean builtins (`prob`, `istype`, `ispath`, `isnum`, `istext`, `islist`, `hascall`) are already `bool` and stay as they are;
- anything else becomes `Dm.truthy(e)`. Overloads: `float` (`!= 0`), `float?` (non-null and `!= 0`), `string` (non-null and non-empty), `object` (null, `0` and `""` are false, everything else true; an empty list is true), and any class, `DmList` or `Type` (non-null).
- `!e` becomes `!` of the converted condition; in a value context (assigned, returned, passed) `!e` is `Dm.not(e)`, a DM number 1 or 0.
- a C# `bool` in a value context becomes `Dm.num(b)`.
- a constant true loop condition, `while(1)` or `while(TRUE)`, is `while (true)` (`sorts/sort_instance.dm:391`), not `while (Dm.truthy(1))`.

```
DM:  if(desc)                       C#: if (Dm.truthy(desc))
DM:  if(!anchored && density)       C#: if (!Dm.truthy(anchored) && Dm.truthy(density))
DM:  . = (health > 0)               C#: dot = Dm.num(health > 0);
DM:  return !QDELETED(src)          C#: return Dm.num(!QDELETED(this));
```

`x != ""` alone is never a truthiness test: it is true for a null string.

### 4.2 Equality and comparison

| DM | both operands are `float`/`int`, both `string`, or both references (class, `DmList`, `Type`) | any operand is `object`, `float?`, or the kinds differ |
|---|---|---|
| `a == b` | `a == b` | `Dm.eq(a, b)` |
| `a != b`, `a <> b` | `a != b` | `!Dm.eq(a, b)` |
| `a < b`, `<=`, `>`, `>=` | `float` operands: `a < b`; `string` or reference operands: `Dm.lt(a, b)`, `Dm.le`, `Dm.gt`, `Dm.ge` | a `float?` operand with a `float` or `float?` other operand: `Dm.num(a) < b`; an `object` operand: `Dm.lt(a, b)`, `Dm.le`, `Dm.gt`, `Dm.ge` (null counts as 0, text compares as text) |
| `a <=> b` | `Dm.cmp(a, b)` | `Dm.cmp(a, b)` |
| `x == null`, `isnull(x)` | `x == null` | `x == null` |

`Dm.eq` is DM equality: numbers by value whatever their box (`float`, `int`), text by ordinal comparison, everything else by reference; values of different kinds are never equal and never raise (`null == 0` is false, `null == ""` is false, `"1" == 1` is false). C# `object == object` compares references, so boxed `1f == 1f` is false in C#: never use `==` on an `object` operand. `CS0252`, `CS0253` and `CS0472` are errors in the Tg project.

```
DM:  if(state == SS_RUNNING)            (state is float)        C#: if (state == SS_RUNNING)
DM:  if(thing.process(wait * 0.1) == PROCESS_KILL)   (process returns object)
C#:  if (Dm.eq(thing.process(wait * 0.1f), PROCESS_KILL))
```

### 4.3 Arithmetic

- With only `float` / `int` operands: C# operators (`+ - *`, unary `-`). With a `float?` operand: wrap it, `Dm.num(x) + 1`. With an `object` operand: `Dm.add(a, b)`, `Dm.sub`, `Dm.mul`, `Dm.div`, `Dm.neg(a)`; they implement DM's full table (numbers, text concatenation for `+`, list `+`/`-`, matrix operations, null as 0).
- **Division.** `a / b` where `b` is a nonzero numeric literal or a constant (a `#define` or `const`) is C# `a / b`. Any other divisor: `Dm.div(a, b)`, which raises DM's "Division by zero" runtime error (`float` operands give a `float`, any `object` operand gives an `object`). If both operands are of C# type `int` (two int constants), write `(float)a / b`: DM division is always real division.
- `a % b` is `Dm.mod(a, b)` (both operands truncated to integers, result has the dividend's sign; BYOND ref `%`). `a %% b` is `Dm.fmod(a, b)` (`B * fract(A / B)`, the dividend's sign; BYOND ref `%%`). `a ** b` is `Dm.pow(a, b)`.
- **Text concatenation** `a + b` with two `string` operands is C# `a + b` (null concatenates as nothing in both languages). A `string` with a non-`string` operand is `Dm.add(a, b)`: DM raises a type-mismatch error for `"x" + 1`, C# would concatenate silently.
- `x++`, `x--`, `++x`, `--x` on a `float`: as is. On `float?` or `object`: the statement `Dm.inc(ref x);` / `Dm.dec(ref x);` (DM coerces any non-number, null included, to 0 first); in a value context `Dm.postinc(ref x)` / `Dm.preinc(ref x)` (and `dec`).
- **Compound assignment** on a `float` target with a `float` operand: as is (`x += 2;`, `x *= y;`, `x /= 2;`, but `x = Dm.div(x, y);` under the division rule). On other targets see section 6.3 (lists) and this table:

| DM | `float` target | `float?` / `object` target | `string` target | `DmList` target |
|---|---|---|---|---|
| `A += B` | `A += B` (`B` converted to `float`) | `Dm.append(ref A, B)` | `A += B` when `B` is `string`, else `A = (string)Dm.add(A, B)` | `Dm.append(ref A, B)` |
| `A -= B` | `A -= B` | `Dm.remove(ref A, B)` | - (DM raises) | `Dm.remove(ref A, B)` |
| `A *= B`, `A /= B` | `A *= B`, division rule | `object` target: `A = Dm.mul(A, B)`, `A = Dm.div(A, B)`; `float?` target: `A = Dm.num(A) * B`, and the division rule with `Dm.num(A)` (`A = Dm.num(A) / B` for a constant divisor, else `A = Dm.div(Dm.num(A), B)`), `B` converted to `float` by 3.5 (`Dm.mul` and `Dm.div` with an `object` operand return `object`, which a `float?` does not accept) | - | - |
| `A \|= B` | `Dm.combine(ref A, B)` | `Dm.combine(ref A, B)` | - | `Dm.combine(ref A, B)` |
| `A &= B` | `Dm.mask(ref A, B)` | `Dm.mask(ref A, B)` | - | `Dm.mask(ref A, B)` |
| `A ^= B` | `Dm.bitxor_assign(ref A, B)` | same | - | same |
| `A <<= n`, `A >>= n` | `A = Dm.shl(A, n)`, `A = Dm.shr(A, n)` | same with `Dm.num(A)` | - | - |
| `A %= B`, `A %%= B` | `A = Dm.mod(A, B)`, `A = Dm.fmod(A, B)` | same | - | - |

  `ref` needs a field, a local or a param. Two other targets occur (about 1,100 compound assignments on list elements and 700 on engine lists in tg), and both keep DM's in-place list semantics:

  - **A list element** `L[i]`: `L[i] += B` is `Dm.append_at(L, i, B)`, and `-=`, `|=`, `&=`, `^=` are `Dm.remove_at`, `Dm.combine_at`, `Dm.mask_at`, `Dm.bitxor_assign_at`. Each reads the element, applies exactly what the `ref` form does to it (a list element is changed in place, so other references to that inner list see it, as tg's `LAZYADDASSOCLIST` relies on; a null element becomes `B` itself; a number is added), and stores the result back with the indexer's assignment rules. `L` and `i` are ordinary arguments, so each is evaluated once. `L[i]++` / `L[i]--` as statements are `Dm.inc_at(L, i)` / `Dm.dec_at(L, i)`, in a value context `Dm.postinc_at`, `Dm.preinc_at`, `Dm.postdec_at`, `Dm.predec_at` (`switch(++BadBoy.failure_strikes[BadBoy.type])`, master.dm:300). The other operators (`*=`, `/=`, `%=`, `<<=`, ...) are written `L[i] = Dm.mul(L[i], B)`, with `L` and `i` bound to temps unless they are plain reads (4.12).
  - **An engine-list property** (`contents`, `overlays`, `underlays`, `vis_contents`, later `client.screen` and `client.images`; never null, `docs/ai/specs/object-model.md` section 2): `A.overlays += x` is `Dm.append(A.overlays, x)`, the overload without `ref`, which changes the list in place; likewise `Dm.remove`, `Dm.combine`, `Dm.mask`, `Dm.bitxor_assign`. A `float` property (`pixel_x`, `alpha`, `layer`) takes C# compound operators like a field; an `object` property (`color`, `transform`) is written `A.transform = Dm.mul(A.transform, B)` with `A` bound to a temp unless it is a plain read.

```
DM:  L[K] += list(V)                    (LAZYADDASSOCLIST, __HELPERS/_lists.dm:80) C#: Dm.append_at(L, K, Dm.list(V));
DM:  used_key_list[input_key]++        (__HELPERS/_lists.dm:931)                  C#: Dm.inc_at(used_key_list, input_key);
DM:  overlays += build_appearance_list(add_overlays)   (overlays.dm:82)             C#: Dm.append(overlays, build_appearance_list(add_overlays));
```

### 4.4 Bit operations are 24-bit

DM bit operators work on 24-bit unsigned integers (BYOND ref `&`, `|`, `^`: "A and B must be between 0 and 2**24 - 1"; OpenDream masks with `0x00FFFFFF`). Every bit operation is a shim call, whatever the operand types:

| DM | C# |
|---|---|
| `a & b`, `a \| b`, `a ^ b` | `Dm.bitand(a, b)`, `Dm.bitor(a, b)`, `Dm.bitxor(a, b)` (numbers: 24-bit; lists: section 6.3) |
| `~a` | `Dm.bitnot(a)` (`(~a) & 0xFFFFFF`) |
| `a << n`, `a >> n` | `Dm.shl(a, n)` (`(a << n) & 0xFFFFFF`), `Dm.shr(a, n)` (`(a & 0xFFFFFF) >> n`) |

```
DM:  if(flags_1 & INITIALIZED_1)          C#: if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
DM:  datum_flags &= ~DF_USE_TAG           C#: Dm.mask(ref datum_flags, Dm.bitnot(DF_USE_TAG));
DM:  obj_flags |= CAN_BE_HIT              C#: Dm.combine(ref obj_flags, CAN_BE_HIT);
```

Constant expressions inside `#define`s are folded by the defines converter with the same masks (`ALL (~0)` is `0xFFFFFF`).

### 4.5 `&&` and `||` as values

DM `a || b` returns the first true operand or the last false one; `a && b` returns the first false operand or the last one (BYOND ref `||`, `&&`). In a condition they are C# `&&`/`||` over converted conditions (4.1). In a value context (assigned, returned, passed, stored), with a C# pattern temp so `a` is evaluated once and `b` only when needed:

```
DM:  sound_to_use.falloff = max_distance || 1                  (sound.dm:155)
C#:  sound_to_use.falloff = (max_distance is var or__1 && Dm.truthy(or__1) ? or__1 : 1);
DM:  return trimtext(text) || ""
C#:  return (trimtext(text) is var or__1 && Dm.truthy(or__1) ? or__1 : "");
DM:  var/x = a && b
C#:  object x = (a is var and__1 && !Dm.truthy(and__1) ? and__1 : b);
```

When the two operands have different C# types, cast the second to `object` (`: (object)b`). C# `??` is never a translation of `||` (it is wrong for 0 and `""`).

When **both** operands are conditions that 4.1 turns into a C# `bool` (comparisons, `Dm.eq`, type tests, `!e`, `x == null`), the DM value is 1 or 0 and the whole expression is `Dm.num(a || b)` / `Dm.num(a && b)`, with no temp:

```
DM:  var/is_multi_tile = bound_width > ICON_SIZE_X || bound_height > ICON_SIZE_Y   (atoms_movable.dm:1174)
C#:  float is_multi_tile = Dm.num(bound_width > ICON_SIZE_X || bound_height > ICON_SIZE_Y);
```

**`||=` and `&&=`.** As a statement, `a ||= b` is `if (!Dm.truthy(a)) a = b;` and `a &&= b` is `if (Dm.truthy(a)) a = b;`. Used as a value (BYOND ref `||=`: the value of `A || B` after the assignment), it is the 4.5 form with the assignment in the second branch: `(a is var or__1 && Dm.truthy(or__1) ? or__1 : (a = b))` and `(a is var and__1 && !Dm.truthy(and__1) ? and__1 : (a = b))`. In both forms `a`'s container and index are bound to temps first unless they are plain reads (4.12).

```
DM:  var/list/procs = (_signal_procs ||= list())                         (datums/signals.dm:32)
C#:  DmList procs = (_signal_procs is var or__1 && Dm.truthy(or__1) ? or__1 : (_signal_procs = Dm.list()));
DM:  var/list/target_procs = (procs[target] ||= list())                  (datums/signals.dm:33)
C#:  DmList target_procs = (procs[target] is var or__2 && Dm.truthy(or__2) ? or__2 : (procs[target] = Dm.list())) as DmList;
```

### 4.6 `round` and math builtins

- `round(A)` is floor (`round(a)`; the BYOND ref calls the one-argument form deprecated in favour of `floor`). `floor`, `ceil`, `trunc`, `fract`, `sign`, `abs`, `sqrt`, `min`, `max`, `clamp`, `log`, `lerp` keep their DM calls.
- **Signatures.** The numeric builtins (`round`, `floor`, `ceil`, `trunc`, `fract`, `sign`, `abs`, `sqrt`, `log`, `lerp`, the trigonometric procs) take `float` parameters and return `float`; an argument of another C# type is converted by 3.5 (`ceil(Dm.num(wait))` for a `float?` `wait`). `min`, `max` and `clamp` have an overload whose parameters are all `float` (returns `float`), used when every argument is `float` or `int`, and an `object` overload with DM's full rules (text compares as text, a single list argument, null), used otherwise (returns `object`). The text and list builtins of Appendix C.1 take `object` where DM accepts several kinds and return the kind DM returns (`length` and `findtext` return `float`, `copytext` returns `string`).

```
DM:  wait = max(ceil(wait), 1)                                  (timer.dm:623; wait is float?)
C#:  wait = max(ceil(Dm.num(wait)), 1);
DM:  wait = max(CEILING(wait, world.tick_lag), world.tick_lag)  (timer.dm:625; CEILING(float x, float y) returns float, 11.1)
C#:  wait = max(CEILING(Dm.num(wait), world.tick_lag), world.tick_lag);
```
- `round(A, B)` is the nearest multiple of B with **ties rounded up**: `floor(A / B + 0.5) * B`, with `A / B + 0.5`, the floor and the product computed in `double` and the result rounded to `float` once, which is what `Dm.round` does (in `float` there are no halves from 2^23 up, so `round(8388609, 1)` would give 8388610 and a value that is already a multiple of B would move to the next one). The BYOND ref says only "the nearest multiple"; tg documents the BYOND tie behaviour at `__DEFINES/atmospherics/atmos_core.dm:41-42` ("When it has a second arg it will round up. So for instance round(0.5, 1) == 1"). OpenDream uses `Math.Round` (ties to even) here and is not followed. Conformance test: `round(0.5, 1) == 1`, `round(2.5, 1) == 3`, `round(-0.5, 1) == 0`, `round(8388609, 1) == 8388609`.
- `sin`, `cos`, `tan`, `arcsin`, `arccos`, `arctan` work in degrees, as in DM.
- C# `Math.Round`, `MathF.Round`, `(int)` casts and integer division are never used for DM numbers.

### 4.7 `in`

- `A in L` is `Dm.@in(A, L)`: false when `L` is null or not a list; for an atom it tests `L.contents`; for an associative list it tests the keys; the world (`A in world`) tests world membership. Never `L.Contains(A)`: it throws on a null `L`, and tg's lazy lists are null whenever they are empty.
- `A in X to Y` (a range test outside a `for` and outside `switch`) is `Dm.in_to(A, X, Y)` (inclusive; false for a non-number). Not `in_range`: tg `#define`s that name (rule 1.6).
- `locate(T) in C` is section 10.4.

```
DM:  if(src in escapee.do_afters)        (disposal/holder.dm:135)    C#: if (Dm.@in(this, escapee.do_afters))
DM:  if(icon_x in 12 to 20)                                          C#: if (Dm.in_to(icon_x, 12, 20))
```

### 4.8 Member access

- `a.b` and `a.b()` on a typed `a`: C# `a.b`. `src.b` is `this.b`; a bare `b` is `b`.
- `a?.b`, `a?.b()`, `L?[i]`: C# `?.` and `?[]` (C# 14 null-conditional assignment matches DM: when `a` is null the assignment and its right-hand side are skipped). A numeric `a?.b` in a numeric context is `Dm.num(a?.b)`.
- `a:b` (runtime lookup by name) and `a?:b`: always dynamic, `Dm.get_var(a, "b")`, `Dm.set_var(a, "b", v)`, `Dm.Call(a, "b", args...)`; the `?:` forms return null for a null `a` (`Dm.get_var_or_null(a, "b")`). (Not `Dm.get`: tg has a global proc `get`, rule 1.6.) (about 190 `:` sites at the snapshot, many inside macros.)
- `a.b` where `b` is a `var/static` or `var/global` of `a`'s type: `<DeclaringClass>.b` (section 10.6); `a` is evaluated first in its own statement only if it has side effects.
- `::A` / `global.A` (global disambiguation): `GlobalProcs.A` for a proc, the GLOB or static accessor for a var. `T::v` and `x::v`: section 10.3.

### 4.9 `?:` ternary

`c ? a : b` is `(<condition c> ? a : b)` with `c` converted by 4.1. When `a` and `b` have different C# types, cast one to `object`.

```
DM:  . = anchored ? "secured" : 0
C#:  dot = (Dm.truthy(anchored) ? (object)"secured" : 0);
```

### 4.10 `~=` and `~!`

`a ~= b` is `Dm.equiv(a, b)` and `a ~! b` is `!Dm.equiv(a, b)`: two lists are equivalent when their items are equal in order and, as in BYOND 516+, their associated values are equal (shallow); matrices compare components; other values fall back to `Dm.eq` (BYOND ref `~=`). `~=` has nothing to do with text case; v0's claim was wrong. All 9 tg uses are on lists.

```
DM:  if(old_list ~= new_list)       C#: if (Dm.equiv(old_list, new_list))
```

### 4.11 Precedence

The translator parses with DM precedence (BYOND ref "operators": `* / % %%` > `+ -` > `< <= > >= <=>` > `<< >>` > `== != <> ~= ~!` > `&` > `^` > `|` > `&&` > `||` > `?:` > assignment > `in`) and writes C# that has the same tree. Two differences from C# matter:

1. **Shifts bind more loosely than relational operators in DM.** All shifts are shim calls (4.4), so the DM operands become explicit call arguments. No tg site mixes an unparenthesised shift with a comparison at the snapshot.
2. **`in` binds more loosely than everything, assignment included.** `A in L || B` is `A in (L || B)`; `x = A in L` is `(x = A) in L` (a statement with no effect beyond the assignment). Five tg sites read differently from how they parse; translate the DM parse, not the intent, and add `// DM parse: ...`:

```
code/game/objects/items/stacks/stack.dm:575   if(locate(/obj/structure/transport/linear/tram) in dest_turf || locate(/obj/structure/thermoplastic) in dest_turf)
code/datums/components/crafting/_crafting.dm:388   (the same expression)
code/modules/mob/inventory.dm:301             if(locate(/obj/structure/table) in turf || locate(/obj/structure/rack) in turf || ...)
code/modules/spells/spell_types/pointed/_pointed.dm:78   return locate(/mob/living/carbon/human) in target || locate(/mob/living) in target
code/datums/elements/food/processable.dm:68   var/found_tray = locate(/obj/item/storage/bag/tray) in found_location || locate(/obj/item/plate/oven_tray) in found_location
```

```
DM:  if(locate(/obj/structure/transport/linear/tram) in dest_turf || locate(/obj/structure/thermoplastic) in dest_turf)
C#:  // DM parse: locate(tram) in (dest_turf || (locate(thermoplastic) in dest_turf)); only the tram is looked for.
     if (Dm.truthy(Dm.locate<obj_structure_transport_linear_tram>(dest_turf is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.locate<obj_structure_thermoplastic>(dest_turf))))
```

"Fixing" such a site is a deviation: it needs a `// DEVIATION(DVn): ...` comment and a line in `docs/ai/DEVIATIONS.md` (section 14). The `!A in L` trap (`!` binds tighter than `in`) does not occur in tg. Unparenthesised `|` inside `#define` bodies (`ZAP_DEFAULT_FLAGS`, defines.md item 5) is folded by the converter with DM precedence.

### 4.12 Order and count of evaluation

C# evaluates operands and arguments left to right, like DM. A DM expression evaluated once must be evaluated once in C#: when a translation evaluates an operand more than once (a compound assignment rewritten as `L[i] = Dm.mul(L[i], B)`, a `switch` lowered to `if` chains, an inline macro whose expansion evaluates an argument more than once), bind the operand to a temp `t__<n>` first unless it is a **plain read**: a local, a parameter, `this`, a constant, or a field read whose receiver is itself a plain read (`user.mind`). A temp is declared right before the statement with the operand's C# type.

What counts is how often the expansion **evaluates** the argument, not how often its text names it: a macro that names an argument in both branches of a ternary evaluates it once, so the argument is substituted literally, side effects included. tg relies on it: `#define fetchElement(L, i) (associative) ? L[L[i]] : L[i]` (`sorts/sort_instance.dm:2`) is used as `fetchElement(L,runHi++)` (`:146`), which increments once:

```
DM:  var/current = fetchElement(L,runHi++)               (sorts/sort_instance.dm:146)
C#:  object current = (Dm.truthy(associative) ? L[L[Dm.postinc(ref runHi)]] : L[Dm.postinc(ref runHi)]);
```

```
DM:  weights[get_key(thing)] *= 2
C#:  object t__1 = get_key(thing);
     weights[t__1] = Dm.mul(weights[t__1], 2);
```

### 4.13 Randomness

`prob(p)`, `rand()`, `rand(a, b)`, `pick(a, b, ...)`, `pick(L)` and `roll(...)` are the bare builtins (Appendix C) and draw from the world RNG (`World.Rng`, reseeded every tick, D31); `System.Random`, `Random.Shared`, `Guid` and hash codes are never sources of randomness. Arguments are evaluated left to right before the draw, as in DM. A weighted pick lists (weight, value) pairs, an unweighted item weighing 100 (BYOND ref "pick proc"):

```
DM:  pick(20;"brown", 5;"blue", "green")         (__HELPERS/mobs.dm:39 style)
C#:  Dm.pick_weighted((20, "brown"), (5, "blue"), (100, "green"))
```

tg's own helpers (`pick_weight(L)`, `pick_n_take(L)`, `SPT_PROB(...)`) are translated tg code.

---

## 5. Text

### 5.1 Literals

A DM text literal without embedded expressions and without text macros becomes a C# string literal holding the same characters:

| DM | C# literal |
|---|---|
| `\"`, `\\`, `\n`, `\t` | `\"`, `\\`, `\n`, `\t` |
| `\[`, `\]` | `[`, `]` |
| `{"..."}` (document string, escapes and expressions still apply) | a regular C# literal with the line breaks as `\n` |
| `@"..."`, `@{"..."}`, `@(XYZ)...XYZ` (raw, no escapes) | C# verbatim `@"..."` (inner `"` doubled) or a raw literal `"""..."""` when it spans lines |
| `\` at end of line (continuation) | joined, leading spaces of the next line dropped |

A literal that contains any other escape (`\<`, `\>`, `\xNN`, `\uNNNN`, `\(space)`) or a text macro (5.4) goes through `Dm.text` (5.2) even when it has no embedded expression: `Dm.text("\\improper CPU")`. C# interpolated strings (`$"..."`) are never used for DM text.

### 5.2 Embedded expressions: `Dm.text`

A DM text with embedded expressions `"...[e1]...[e2]..."` that is **not** player-visible (5.3) becomes `Dm.text(template, e1, e2)`, where the template is the DM literal's inner text, verbatim and in DM syntax, with each `[expr]` replaced by `[]` (escaped for C#: `\\` for each DM backslash). This is exactly DM's builtin `text(FormatText, Args)` (BYOND ref `text proc`), so the rendering rules are DM's: numbers with 6 significant digits, null as `""`, a typepath as its path, an atom as its name with `\the`/`\The` inserted when no article is given, text macros applied, `\[` kept literal.

```
DM:  "cooldown_[cd_index]"                        C#: Dm.text("cooldown_[]", cd_index)
DM:  "[thing.type] ([REF(thing)])"                C#: Dm.text("[] ([])", thing.type, REF(thing))
DM:  text("[] is []", a, b)                       C#: Dm.text("[] is []", a, b)
```

### 5.3 Player-visible text: `I18n.T` [C0]

Player-visible text is any text that reaches a player as words: an argument (directly, through `span_*`, or through a local that only collects message text) of `to_chat`, `visible_message`, `audible_message`, `balloon_alert`, `balloon_alert_to_viewers`, `say`/`emote` message arguments, `examine`/`examine_more` return lists, `alert`/`tgui_*` texts, and runtime assignments to `name`, `desc`, `real_name` and the other player-facing vars of an **atom** listed in `docs/ai/specs/presentation-seam.md` (a non-atom datum's `name`, such as the typepath label of `/datum/qdel_item`, `controllers/subsystem/garbage.dm:341`, is not player-visible: `name = Dm.text("[]", mytype);`). Log lines, admin messages, keys, ids, `REF()`s, file paths, icon states, signal names and trait names are not player-visible.

Rule: every player-visible DM text, literal or interpolated, becomes `I18n.T(template, args...)` with the template built exactly as in 5.2. A nested player-visible literal inside an embedded expression is itself wrapped. `I18n.T` returns the same English `string` that `Dm.text` would (core logic keeps comparing, concatenating and storing plain English text); its template is the localization key (D17, D75) and the arguments are the parameters.

```
DM:  to_chat(user, span_notice("You start prying [src] with [I]..."))
C#:  to_chat(user, span_notice(I18n.T("You start prying [] with []...", this, I)));
DM:  to_chat(user, span_notice("You [anchored ? "unsecure" : "secure"] [src]."))
C#:  to_chat(user, span_notice(I18n.T("You [] [].", Dm.truthy(anchored) ? I18n.T("unsecure") : I18n.T("secure"), this)));
DM:  balloon_alert(user, "can't reach!")
C#:  balloon_alert(user, I18n.T("can't reach!"));
DM:  name = "broken [initial(name)]"
C#:  name = I18n.T("broken []", Dm.initial(this, (atom p) => p.name));
```

Type-level defaults of `name`, `desc` and similar vars are **not** wrapped: they are data, written as plain literals (or `Dm.text(...)` when they contain a text macro) and extracted by the type-tree converter as keys `(type, var)`. How the template and arguments travel to the client (a provenance table keyed by the returned string that the presentation seam reads, or a structured text value) is decided with the client localization in M9; the call form above does not change with that decision.

The default separator and filler words of tg's text helpers that join pieces (`english_list(list/input, nothing_text = "nothing", and_text = " and ", comma_text = ", ", final_comma_text = "")`, `__HELPERS/_lists.dm:243`, and `type_english_list`, `:265`) are plain literals too: they are data that reaches a player only through a caller's message, which the caller wraps. How a joined list is localized (word order, conjunctions) is an open item of `docs/ai/specs/presentation-seam.md`.

### 5.4 Text macros

`\the`, `\The`, `\a`, `\an`, `\A`, `\An`, `\he`, `\He`, `\she`, `\his`, `\His`, `\him`, `\himself`, `\herself`, `\hers`, `\proper`, `\improper`, `\th`, `\s`, `\icon`, `\ref`, `\roman`, `\Roman` (BYOND ref "macros (text)") stay in the template in DM spelling and are applied by `Dm.text`/`I18n.T`, which use the atom's `name`, `gender` and the proper-noun rule (a name starting with a capital is proper unless marked `\improper`). Inside stored text (`name = "\improper CPU"`) the shim encodes `\proper` and `\improper` as private-use marker characters, which display code strips; translated code never builds or parses the markers. `\ref[x]` is `REF`-style: section 10.

### 5.5 Numbers in text and text to numbers

`"[x]"` (also inside a template, `Dm.str`, `Dm.text`, `I18n.T`) and `num2text(x)` render numbers like DM: 6 significant digits, `%g` style, trailing zeros removed, a lowercase exponent with at least two digits (`1e-06`, `1e+08`), `inf`, `-inf` and `nan`, invariant culture (3.9). Where OpenDream's BYOND-checked tests contradict the reference prose, the port follows the tests: in `"[x]"` a whole number from 1e6 up to 2^24 prints in full (`"[1000000]"` is `"1000000"`, `"[16777216]"` is `"16777216"`, but `"[16777218]"` is `"1.67772e+07"`; OpenDream `DreamValue.Stringify`, test `Math/Text.dm`), while `num2text(1000000)` keeps `"1e+06"` (OpenDream test `num2text`); the reference shows one million as `1e+06` in both. OpenDream applies the whole-number exception to positive numbers only, so `"[-1000000]"` is `"-1e+06"`, an asymmetry the port copies. None of this is verified against BYOND 516; a run on BYOND settles it. `num2text(N, SigFig)` and `num2text(N, Digits, Radix)` keep their DM calls; `text2num(t)` returns null when `t` is not a number.

### 5.6 Length and character procs [C0]

- `length(t)` on text, and the byte-indexed text procs (`copytext`, `findtext`, `findtextEx`, `findlasttext`, `replacetext`, `splicetext`, `spantext`, `nonspantext`), count **UTF-16 code units** in the port, where BYOND counts UTF-8 bytes. For ASCII text they agree; for Cyrillic text a byte limit such as `length(name) > MAX_NAME_LEN` admits twice as many letters (DV4).
- The `_char` procs (`length_char`, `copytext_char`, `findtext_char`, ...) count Unicode scalar values (`System.Text.Rune`), exactly as BYOND counts characters.
- DM 515's text index `T[i]` is `Dm.textchar(T, i)`: the character that starts at the UTF-16 position `i` (a character outside the BMP is its two units, so a loop stepping by `length(char)` walks characters as BYOND's byte loop does); an index outside the text is BYOND's runtime error.

```
DM:  if(length_char(new_name) > MAX_NAME_LEN)    C#: if (length_char(new_name) > MAX_NAME_LEN)
DM:  copytext(key, 1, 5)                         C#: copytext(key, 1, 5)
```
- `findtext`, `replacetext`, `cmptext` and `sorttext` are case-insensitive; the `Ex` variants and `==` are case-sensitive; text keys in lists are case-sensitive.

---

## 6. Lists, loops and switch

### 6.1 Creating lists

| DM | C# |
|---|---|
| `list()` | `Dm.list()` |
| `var/list/x = new` (the implied type `/list`, no arguments; `__HELPERS/_lists.dm:471`) | `DmList x = Dm.list();` (an empty list, as `list()`) |
| `list(a, b, c)` | `Dm.list(a, b, c)` (a list argument becomes a nested list, not flattened) |
| `list("a" = 1, b = 2, (x) = 3, /obj/item = 4)` | `Dm.list(Dm.kv("a", 1), Dm.kv("b", 2), Dm.kv(x, 3), Dm.kv(typeof(obj_item), 4))` (a bare identifier key is its text; a parenthesised key is its value) |
| `list(a, "k" = v)` (mixed) | `Dm.list(a, Dm.kv("k", v))` |
| `new /list(n)`, `var/L[n]` | `Dm.list_n(n)` (n nulls) |
| `new /list(a, b, ...)` (several sizes), `var/L[a][b]` | `Dm.@new<DmList>(a, b, ...)`: a list of `a` lists of `b` items each, one level per size, the last level of nulls, a negative or non-number size counting as 0 (BYOND ref `/list`: `new/list(10,5)` is "a list of 10 lists each having 5 elements"; the binder builds the nested lists). tg: `clients_by_zlevel = new /list(world.maxz,0)` (`controllers/subsystem/mobs.dm:22-23`, `ai_controllers.dm:149`, one empty list per z-level) is `clients_by_zlevel = Dm.@new<DmList>(world.maxz, 0);`, `var/grid[max_x - min_x + 1][max_y - min_y + 1]` (`admin/verbs/mapping.dm:273`) is `DmList grid = Dm.@new<DmList>(max_x - min_x + 1, max_y - min_y + 1);` |
| `alist(...)` (BYOND 516 associative list, 73 tg uses; `GLOBAL_ALIST_*`) | `Dm.alist(...)` with the argument forms of `Dm.list`; it returns a `DmList` in alist mode, where a number used as a key is a key, not an index, and keys are unique (BYOND ref `alist`); BYOND leaves the key order unspecified, the port keeps insertion order so that iteration is deterministic; `var/alist/x` is typed `DmList` like `var/list/x` |
| `L.Copy()`, `L.Copy(a, b)` | `L.Copy()`, `L.Copy(a, b)` |

C# collection initializers (`new DmList { ... }`) are never used: they call `Add`, which flattens list arguments.

### 6.2 Access

- `L[i]` with a number and `L[key]` with any other value: the `DmList` indexer `L[i]` (1-based; an out-of-range number raises "list index out of bounds"; a missing key reads null; assigning a missing key appends it). The indexer returns `object`; convert by 3.5.
- `L?[i]` is C# `L?[i]`.
- Indexing a value whose C# type is `object` (an `object` local or var, or the result of another index) casts it to `DmList` first: `((DmList)expr)[i]`. A value that is not a list then raises at the cast, as DM raises at the index (9.1). `_signal_procs[target][sig]` (`datums/signals.dm:78`, `_signal_procs` an associative list of lists) is `((DmList)_signal_procs[target])[sig]`; `.len` and the list procs of a nested value cast the same way, `((DmList)_signal_procs[target]).len`.
- `L.len` reads and writes the length (`L.len = 0` clears, a larger value pads with nulls, `L.len--` drops the last item); on a null `L` it is a runtime error, as in DM. `length(L)` is `length(L)` (0 for null).
- List procs keep their DM names as `DmList` methods: `L.Add(...)` (flattens list arguments), `L.Remove(...)` (removes from the end, flattens), `L.RemoveAll(...)`, `L.Cut(a, b)`, `L.Find(x, a, b)`, `L.Insert(i, ...)`, `L.Join(sep, a, b)`, `L.Swap(i, j)`, `L.Splice(a, b, ...)`. Calling them on a null list is a runtime error, as in DM.

```
DM:  var/obj/item/I = held[1]       C#: obj_item I = held[1] as obj_item;
DM:  held["left"] = I               C#: held["left"] = I;
DM:  queue.len--                    C#: queue.len--;
DM:  if(length(L) > 2)              C#: if (length(L) > 2)
```

### 6.3 List operators

DM list operators (BYOND ref "list operators", OpenDream `DreamList`): `+` and `+=` with a list right operand add its items; `-` and `-=` remove, for each item of the right operand, its **last** occurrence (`Remove` starts at the highest index so that it reverses `Add`); the right operand is snapshotted first, so `L -= L` empties `L`; `|`/`|=` add the items not already present; `&`/`&=` keep only items present in both; `^` is the symmetric difference (items only in `A` first, then items only in `B`). `+=`, `-=`, `|=`, `&=` mutate the left list **in place** (other references see the change); `+`, `-`, `|`, `&`, `^` return a new list. With a null left operand, `L += x` makes `L` refer to `x` itself (the same list, not a copy) and `L |= x` likewise.

| DM | C# |
|---|---|
| `L += x` | `Dm.append(ref L, x)` |
| `L -= x` | `Dm.remove(ref L, x)` |
| `L \|= x` | `Dm.combine(ref L, x)` |
| `L &= x` | `Dm.mask(ref L, x)` |
| `L ^= x` | `Dm.bitxor_assign(ref L, x)` |
| `L[i] += x`, `-=`, `\|=`, `&=`, `^=` (a list element) | `Dm.append_at(L, i, x)`, `Dm.remove_at`, `Dm.combine_at`, `Dm.mask_at`, `Dm.bitxor_assign_at` (4.3) |
| `A.overlays += x` and the other engine lists | `Dm.append(A.overlays, x)` (no `ref`; 4.3) |
| `L + x`, `L - x`, `L \| x`, `L & x`, `L ^ x` | `Dm.add(L, x)`, `Dm.sub(L, x)`, `Dm.bitor(L, x)`, `Dm.bitand(L, x)`, `Dm.bitxor(L, x)` |

`DmList` defines no C# operators: a static `operator +` would make `L += x` rebind `L` to a new list, and a C# 14 instance `operator +=` throws on a null `L`. A `DmList`-typed target whose DM value would become a non-list (`null += 5`) raises a runtime error in the port instead of storing the number; tg's lazy lists never do this.

### 6.4 LAZY macros

The `LAZY*` family (`LAZYADD`, `LAZYREMOVE`, `LAZYSET`, `LAZYACCESS`, `LAZYLEN`, `LAZYINITLIST`, `LAZYFIND`, `LAZYNULL`, `LAZYCLEARLIST`, `LAZYADDASSOC`, ...) are hand-expanded `GlobalProcs` members that take the list by `ref` when the macro assigns it: `LAZYADD(ref do_afters, target)`, `LAZYACCESS(L, i)` (section 11).

```
DM:  LAZYADD(user.do_afters, target)        C#: LAZYADD(ref user.do_afters, target);
DM:  if(LAZYACCESS(modifiers, RIGHT_CLICK)) C#: if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
```

### 6.5 `for (... in ...)`

DM iterates a **copy** of the list (except `world`), and a typed loop var adds a hidden `istype` that skips nulls and other types; `as anything` removes that filter (BYOND ref "for list proc").

| DM | C# |
|---|---|
| `for(var/x in L)` | `foreach (object x in Dm.each(L))` |
| `for(var/obj/item/I in L)` | `foreach (obj_item I in Dm.each<obj_item>(L))` (filters by type, skips nulls) |
| `for(var/obj/item/I as anything in L)` | `foreach (obj_item I in Dm.each_cast<obj_item>(L))` (checked cast per element; a wrongly typed element is a runtime error at the loop head, where DM would fail at the first member access) |
| `for(var/datum/wound/W as anything in typecacheof(/datum/wound, FALSE, TRUE))` (typepaths) | `foreach (Type W in Dm.each_cast<Type>(typecacheof(typeof(datum_wound), FALSE, TRUE)))` (V3: the var holds typepaths) |
| `for(var/x as mob\|obj in L)` | `foreach (object x in Dm.each_as(L, DmAs.mob \| DmAs.obj))` |
| `for(var/k, v in L)` | `foreach ((object k, object v) in Dm.pairs(L))` (values are copied with the keys) |
| `for(var/mob/M, v in L)` | `foreach ((mob M, object v) in Dm.pairs<mob>(L))` |
| `for(var/obj/O in T)` (T an atom) | `foreach (obj O in Dm.each<obj>(T))` (iterates `T.contents`) |
| `for(var/mob/M in world)`, `for(var/datum/D in world)` | `foreach (mob M in Dm.each<mob>(world))`: `world.contents`, the live registered atoms of the loop type in id order (live, not copied); atoms only whatever the loop type, so a non-atom loop type finds nothing (tg `debug.dm:1033`, `reference_tracking.dm:101`: "atoms (don't believe its lies)") |
| `for(var/mob/M)`, `for(var/datum/D)` (no `in`) | `foreach (datum D in Dm.each<datum>())`: the world registry, live, in id order: for an atom loop type its atoms (the same objects as `in world`), for `/datum` or a non-atom loop type (tg's `/client` too) its live non-atom datums (tg `debug.dm:1039` counts them after counting the atoms with `in world`; `mass_edit_variables.dm:261`, `reference_tracking.dm:113`, `SDQL_2.dm:664`: "to have it not return atoms") |

`Dm.each` accepts any value: null and non-lists iterate nothing, an atom iterates its `contents` (an area also what stands on its turfs). It is a **copy-on-write snapshot**: the enumerator walks the list's current backing store and registers itself; a mutation while any enumerator is registered swaps in a fresh store, so the enumerator keeps the old items. A loop that does not mutate its list copies nothing. The enumerator stays valid across `await` inside the body and across exceptions (nothing is pooled). Never `foreach` over a `DmList` directly, and never iterate `contents` without `Dm.each`.

**GC timing.** A non-atom datum that nothing references any more stays in the world registry until the C# GC collects it (registry entries are weak, `docs/ai/specs/object-model.md` section 6), so `Dm.each<datum>()` can still find it, where BYOND deletes it as soon as its reference count drops to 0: what a loop without `in` over datums sees depends on GC timing. Atoms stay reachable through their location. tg uses these loops only in admin and debug code (not translated in M0).

**A typed `as anything` loop var that the body type-tests.** tg guards some `as anything` loops with a test of the loop var's own type that skips a wrong item (`for(var/datum/callback/verb_callback/verb_callback as anything in verb_queue)`, `if(!istype(verb_callback))`, `stack_trace(...)`, `continue`: `controllers/subsystem/verb_manager.dm:152-155`; SSinput's `run_verb_queue` likewise). `Dm.each_cast` would raise at the loop head where tg skips the item, so such a loop var is `object` and iterates with `Dm.each`, as a typed local that 3.4 retypes: the test is `x is T` and each access after it casts to the declared type (`foreach (object verb_callback in Dm.each(verb_queue))`, `if (!(verb_callback is datum_callback_verb_callback)) { ...; continue; }`, `((datum_callback_verb_callback)verb_callback).InvokeAsync();`). A loop without such a test keeps `Dm.each_cast`.

A loop var declared before the loop (`var/x` ... `for(x in L)`) is reused: `foreach (object x__it in Dm.each(L)) { x = x__it; ... }`, so `x` keeps the last item after the loop like DM. A loop var that the body assigns (`I += tickersubsystems`, `controllers/master.dm:641`; a C# `foreach` var is read-only) is a local copy of the same counter: `foreach (object I__it in Dm.each(L)) { object I = I__it; ... }`.

### 6.6 Numeric `for` loops

`for(Var in Start to End [step Step])` evaluates `End` and `Step` **once** (BYOND ref "for list proc", numeric loop). End is always hoisted into `<var>__end`:

```
DM:  for(var/i in 1 to length(L))
C#:  for (float i = 1, i__end = length(L); i <= i__end; i++)

DM:  for(var/i in 10 to 1 step -1)
C#:  for (float i = 10, i__end = 1; i >= i__end; i += -1)

DM:  for(var/i in a to b step s)          (s not a constant)
C#:  for (float i = a, i__end = b, i__step = s; i__step >= 0 ? i <= i__end : i >= i__end; i += i__step)
```

A loop var declared before the loop (a local, a parameter or a datum field) is assigned only the values in range, through a `float` counter `<var>__it` as in 6.5:

```
DM:  for(i in 1 to length(L))                          (i declared before the loop)
C#:  for (float i__it = 1, i__end = length(L); i__it <= i__end; i__it++)
     {
         i = i__it;
         ...
     }
```

So after a normal exit the var holds the last value in range (not the first value past `End`), on an empty range it keeps its previous value, and after `break` it holds the current value. BYOND's reference does not say; OpenDream assigns the var only when the range enumerator succeeds (`DreamEnumerators.cs:21-34`, `docs/ai/specs/m0-reading/controllers.md` hazard 5), and tg relies on it: `timer.dm:110`, `:192` and `:292` cut `1` to `i + 1` after the loop, and a datum field as loop var (`for(next_clienttime_timer_index in ...)`) is read by the next fire. The `__it` form needs no special case for a var that is its own `Start` or has C# type `object`:

```
DM:  for(start in start to hi - 1)                     (sorts/sort_instance.dm:105; start and hi are object parameters)
C#:  for (float start__it = Dm.num(start), start__end = Dm.num(Dm.sub(hi, 1)); start__it <= start__end; start__it++)
     {
         start = start__it;
         ...
     }
```

- **`End` and `Step` are numbers.** `i__end` and `i__step` are always `float`: an `End` or `Step` whose C# type is not `float` (an `object` or `float?` expression) is bound with `Dm.num(...)` (3.5), `for(var/i in 1 to value / gcf)` (`__HELPERS/_lists.dm:618`, `value` an `object`) is `for (float i = 1, i__end = Dm.num(Dm.div(value, gcf)); i <= i__end; i++)`.

C-style `for(init, test, inc)` is a C# `for` with the test re-evaluated every iteration (`for(var/i = 1, i <= L.len, i++)` -> `for (float i = 1; i <= L.len; i++)`). `while` and `do ... while` translate directly with 4.1 conditions.

### 6.7 `switch`

DM `switch` compares with `==`, has no fall-through, accepts ranges `if(a to b)` (inclusive) and lists of values, and a `break` or `continue` inside it acts on the enclosing loop.

- Subject of C# type `float` or `string`, all case values constants, no duplicated or overlapping values, and no `break` inside the switch body that targets a loop: a C# `switch` statement. `if(A1, A2)` -> `case A1 or A2:`; `if(1 to 5)` -> `case >= 1 and <= 5:`; `if(null)` -> `case null:` (string subject); `else` -> `default:`; every section ends with `break;` (or `return`/`continue`/`throw`).
- Otherwise (an `object` or other subject, overlapping cases, or a loop `break` inside): an `if`/`else if` chain over a temp `sw__1` holding the subject, comparing with `Dm.eq(sw__1, A)` or `Dm.in_to(sw__1, a, b)` (or `==`/relational operators for a `float` temp), first match wins, `break`/`continue` keep their loop meaning.

```
DM:  switch(hint)                                  (hint is object)
         if(QDEL_HINT_QUEUE) ...
         if(QDEL_HINT_IWILLGC, QDEL_HINT_LETMELIVE) ...
         else ...
C#:  object sw__1 = hint;
     if (Dm.eq(sw__1, QDEL_HINT_QUEUE)) { ... }
     else if (Dm.eq(sw__1, QDEL_HINT_IWILLGC) || Dm.eq(sw__1, QDEL_HINT_LETMELIVE)) { ... }
     else { ... }
```

### 6.8 `break`, `continue`, labels, `goto`

`break` and `continue` keep their DM targets (see 6.7 for `switch`). Labelled loops do occur: `mergeLo` and `mergeHi` label a `while(1)` `outer:` and leave it with `break outer` from inside nested `do ... while` loops (`sorts/sort_instance.dm:390` and `:491`). C# has no labelled `break`, so:

- `break L` is `goto L__break;`, with the label `L__break:` right after the loop (when nothing follows the loop in its block, the label carries an empty statement, `L__break: ;`);
- `continue L` is `goto L__continue;`, with the label `L__continue: ;` as the last statement of the loop body, so that the loop's own step and test run next, as for a `continue`;
- a label is written only when a `goto` uses it (an unused label is warning CS0164). The DM label line itself leaves no C# statement.

```
DM:  outer:                                            (sorts/sort_instance.dm:390)
         while(1)
             ...
             do
                 ...
                 if(len2 == 0)
                     break outer                       (:407)
             while((count1 | count2) < minGallop)
             ...
     if(len1 == 1)
C#:  while (true)
     {
         ...
         do
         {
             ...
             if (Dm.eq(len2, 0))
                 goto outer__break;
         }
         while (Dm.lt(Dm.bitor(count1, count2), minGallop));
         ...
     }
     outer__break:
     if (Dm.eq(len1, 1))
```

DM's `goto` (to a label outside a loop) does not occur in tg's code at the snapshot.

---

## 7. Procs

### 7.1 Slots and the slot registry

DM procs are virtual by name per type tree. A **slot** is one root declaration (the highest type that declares `proc/name`, or a BYOND builtin root such as `/datum/New`) plus every override of it in that type's subtree; it is one C# virtual method. Unrelated roots with the same name (`/datum/controller/proc/Initialize` and `/atom/proc/Initialize`) are different slots and do not influence each other. A builtin root type that tg re-parents keeps its own builtin slots: `/client` has `parent_type = /datum`, but `/client/New` is not an override of `/datum/New` (waiting-model.md 4.1), so its C# method hides the inherited one. It is written `public new virtual` only where C# hides an inherited member of the same signature; where it hides nothing, `new` is warning CS0109 and the method is plain `public virtual`: `/client/New(TopicData)` against `/datum`'s `New(params object[] __extra)`, and `/client/Del`, since the port declares no builtin `/datum/Del` (`slots.tsv` writes `new` for both until its generator follows this rule). Calls by name reach the method either way: the binder takes the most derived declaration of a name.

- Every DM proc is a `public virtual` method in its root and `public override` in the overrides. `virtual` stays even for procs nobody overrides yet.
- **The root's translation fixes the slot's C# signature** (parameter types and names, the `__extra` params array, the return type) **for every override**, by the rules of 3.3, 7.2 and 7.4, applied after a scan of every definition of the slot in the tg tree (`grep -rnE "(^|/)NAME\(" ../tgstation/code --include='*.dm'` plus the definitions inside type blocks, filtered to the root's subtree; `/obj`, `/mob`, `/turf`, `/area` are subtypes of `/atom`).
- The result is generated for every slot in **`docs/ai/specs/generated/slots.tsv`** (`tools/coloring/slots.py`, columns in `docs/ai/specs/colored-procs.md`), which is authoritative: translators copy the signature from there. **Appendix A** is an excerpt (the M0 base slots) plus the per-slot decisions the generator does not make: a narrowed return type (7.4) and a parameter type decided from the `undecided` column (3.3) or retyped by a "-" cell of 3.5. A root's translator who makes such a decision records it in Appendix A in the same commit and reports it to the lead, who updates the generator or the rules; the cross-check in `colored-procs.md` shows every Appendix A row that differs from `slots.tsv`.
- Whether a slot's methods are `DmTask` is decided per slot and comes only from `slots.tsv`: its `signature` column has `DmTask` / `DmTask<object>` exactly when the slot suspends its callers (`suspends = yes`, waiting-model.md 4.2). A slot that is coloured only through `waitfor = FALSE` definitions (`colored = yes` with `suspends = no` in `colored-slots.tsv`: `/datum/process`, `/datum/controller/subsystem/ignite`, `/datum/callback/InvokeAsync`, `/atom/Bumped`, `/atom/LateInitialize`, ...) keeps its synchronous signature, and those definitions get the waitfor wrapper (waiting-model.md 5.4). Being listed in `colored-slots.tsv`, which has a row for every slot, never makes a slot async (section 8).

### 7.2 Parameters, defaults and variadic slots

**Defaults are applied on null, at run time, by a prologue.** In DM a default applies when the argument is omitted *or null*, and the default expression is evaluated on every call (OpenDream `DMProcBuilder`: `IsNull`, `JumpIfFalse`, default expression). C# defaults apply only to omitted arguments and must be constants. So every C# parameter has the neutral default `= null` (3.3), and the first statements of the body assign the DM defaults in declaration order, so a default can use an earlier parameter:

```
DM:  /proc/get_corresponding_wound_type(list/wound_types, mob/living/carbon/victim, severity_min, severity_max = severity_min, list/excluded_zones, ...)   (__DEFINES/wounds.dm:228)
C#:  public static object get_corresponding_wound_type(DmList wound_types = null, mob_living_carbon victim = null, object severity_min = null, object severity_max = null, ...)
     {
         severity_max ??= severity_min;
         ...
DM:  /datum/proc/Destroy(force = FALSE)
C#:  public virtual object Destroy(float? force = null)
     {
         force ??= FALSE;
```

Reads of a `float?` param are `Dm.num(x)` in numeric contexts (4.3), `Dm.truthy(x)` in conditions.

**Variadic slots.** C# overrides cannot add parameters. If the root declares `...`, any definition in the slot reads `args` (7.6), or **any** override in the slot has more parameters than the root (633 of `/atom/proc/Initialize`'s overrides, 159 of `/datum/element/proc/Attach`'s, `/obj/item/stack/use(used, transfer, check)` under `/obj/item/proc/use(used)`), the root gets a trailing `params object[] __extra` and every definition uses the root's C# parameters plus `__extra`.

1. Every override whose DM declaration names fewer, other or more parameters than the slot's C# parameters (compared in order) keeps the slot's C# signature and carries `[DmParams("p1", "p2", ...)]` listing **all** its own DM parameter names in order, or `[DmParams()]` when it declares none; the dynamic binder matches named arguments against it, so a name the override does not declare is a runtime error, as in DM. `/datum/component/ui_host()` (`datums/components/_component.dm:498`, in the slot of `/datum/proc/ui_host(mob/user)`) is `[DmParams()] public override object ui_host(mob user = null)`. An override that declares exactly the slot's parameter names carries no attribute.
2. An override that declares more DM parameters than the root unpacks the extra ones from `__extra` at the top of the body, in order, then applies their defaults: `object a = Dm.arg(__extra, 0);`, `obj_item b = Dm.arg<obj_item>(__extra, 1);` (`as` semantics), `float? c = Dm.arg<float?>(__extra, 2); c ??= TRUE;`. Parameter types follow 3.3 as if the parameter were declared in a root.

```
DM:  /obj/item/stack/use(used, transfer = FALSE, check = TRUE)   (stack.dm:586)
C#:  [DmParams("used", "transfer", "check")]
     public override object use(object used = null, params object[] __extra)
     {
         float? transfer = Dm.arg<float?>(__extra, 0);
         float? check = Dm.arg<float?>(__extra, 1);
         transfer ??= FALSE;
         check ??= TRUE;
```

**Renamed and narrowed parameters.** An override may rename a parameter (C# allows different names in an override) and keeps the slot's C# type. When the DM override declares a narrower type (`mob/living/user` where the slot says `mob user`), the C# parameter keeps the slot type and each member access that needs the narrower type casts at the access, `((mob_living)user).health`: DM checks nothing at entry and fails only at the access.

### 7.3 Named arguments

- **Static call, the name is a C# parameter of the resolved method**: C# named argument. `do_after(user, 5 SECONDS, target = src)` -> `await do_after(user, 5 * SECONDS, target: this)` (`do_after` is a `DmTask` slot).
- **Static call, the name is an extra parameter of an override** (unpacked from `__extra`): converted to its position in that override's `[DmParams]` order, with `null` for skipped positions (null means "use the default", 7.2). `stack.use(5, check = FALSE)` -> `stack.use(5, null, FALSE)`. The position comes from the definition visible from the receiver's static type.
- **Dynamic call** (`Dm.Call`, `Dm.@new`, `CALLBACK`, and the variadic wrappers `AddComponent`, `AddElement`, ... below): each named argument is the item `Dm.named("name", value)`. The binder matches it against the runtime type's definition (`[DmParams]` or the C# parameter names); an unknown name is a runtime error (BYOND ref "named arguments").

```
DM:  new /datum/point(_z = a.z, _pixel_x = px, _pixel_y = py)
C#:  Dm.@new<datum_point>(Dm.named("_z", a.z), Dm.named("_pixel_x", px), Dm.named("_pixel_y", py))
```

A named argument written inside a list instead of a call is a list entry with a text key (section 6.1), and `arglist` turns such entries back into named arguments. tg's `AddElement`, `RemoveElement`, `AddComponent`, `AddComponentFrom` and `LoadComponent` are variadic macros that call a proc of `src` or of an explicit receiver with their arguments wrapped in `list(...)` (`__DEFINES/dcs/helpers.dm:14-26`: `parent.AddComponent(...)` is `parent._AddComponent(list(...))`). They are non-virtual public instance methods of `datum` in the mirror file of that `.dm` (11.1), called bare inside a datum's proc and on the receiver otherwise, as in DM. The caller passes a named argument as a `Dm.named` item, and the method turns it into the text-keyed entry (`Dm.kv`) that DM's `list(...)` makes of it. Each returns what the call it wraps returns (`DmTask` for `_AddElement`, `DmTask<object>` for `_AddComponent` and `_LoadComponent`, nothing for `_RemoveElement`) and is called with the forms of section 8:

```
DM:  AddComponent(/datum/component/overlay_lighting, is_directional = TRUE)   (atoms_movable.dm:211, in the synchronous Initialize)
C#:  Waiting.NoSleep(AddComponent(typeof(datum_component_overlay_lighting), Dm.named("is_directional", TRUE)));
```

Extra positional arguments beyond what a non-variadic static callee declares are dropped (DM ignores them unless the callee reads `args`, and a callee that reads `args` is variadic by 7.6).

**`arglist`.** `arglist(L)` is only ever the whole argument list of a call (BYOND ref `arglist`). A call of a proc with `arglist(L)` cannot bind statically, so it is a call by name: `f(arglist(L))` is `Dm.Call(this, nameof(f), Dm.arglist(L))`, `obj.f(arglist(L))` is `Dm.Call(obj, nameof(<class of obj>.f), Dm.arglist(L))`, a global proc is `Dm.Call(null, nameof(GlobalProcs.f), Dm.arglist(L))`; like every `Dm.Call` it returns `DmTask<object>` and takes the forms of section 8 (`object result = Waiting.NoSleep(Dm.Call(A, nameof(atom.Initialize), Dm.arglist(arguments)));` in the synchronous `InitAtom`, atoms_initializing_EXPENSIVE.dm:17; `await Dm.Call(this, nameof(Moved), Dm.arglist(__move_args));` in `RESOLVE_ACTIVE_MOVEMENT`). `new T(arglist(L))` is `Dm.@new<T>(Dm.arglist(L))`. A builtin that BYOND lets take `arglist` (`min`, `max`, `filter`, `generator`, `icon.MapColors`) receives `Dm.arglist(L)` as its only argument and expands it itself. `..(arglist(L))` does not occur in tg.

### 7.4 Return type and `.`

The slot's return type (7.1), decided from every definition of the slot:

- `void` when no definition produces a value of its own: no `return <expr>` and no assignment to `.` other than `. = ..()` / `return ..()`. Also `/datum/New` (its value is discarded by `new`).
- otherwise `object`, unless Appendix A narrows it: `float`, `string`, `DmList`, `Type` or a class, allowed only when every exit of every definition returns that kind and **no exit can return null** (no bare `return`, no fall-off with `.` unset, no `return null`). `RETURN_TYPE(path)` in the root supports narrowing to that class under the same condition.
- slots that suspend their callers (`suspends = yes`: the `DmTask` signature of `slots.tsv`, 7.1) return `DmTask` / `DmTask<T>` with the same `T` (section 8); a slot coloured only through `waitfor = FALSE` definitions keeps `void` / `T`.

The DM `.` var is a local `dot` of the return type (for a `DmTask<T>` slot, its `T`), declared at the top of the body (`object dot = null;`) when the proc reads or writes `.`. `return` and falling off the end return `dot` (or `null` when `.` is unused; nothing for `void`). `return x` is `return x;` converted by 3.5. In a `set waitfor = FALSE` definition, `.` lives in the wrapper's `DotBox` (`docs/ai/specs/waiting-model.md`).

**A list-valued `.`.** When every write to `.` in the proc stores a list or null (`. = list()`, `. = <an expression of C# type DmList>`, `. = null`, and the list operators on it: `.[k] = v`, `. += x`, `. -= k`), `dot` is declared `DmList dot = null;` (in an `object` slot as well), so that it is indexed and passed as a list directly. Otherwise `dot` has the return type and every list use casts at the use, `((DmList)dot)[k]` (6.2).

```
DM:  /proc/make_associative(list/flat_list)            (__HELPERS/_lists.dm:938)
         . = list()
         for(var/thing in flat_list)
             .[thing] = TRUE
C#:  public static object make_associative(DmList flat_list = null)
     {
         DmList dot = null;
         dot = Dm.list();
         foreach (object thing in Dm.each(flat_list))
             dot[thing] = TRUE;
         return dot;
     }
```

```
DM:  . = ..()                (in an Initialize(mapload) override)   C#: dot = base.Initialize(mapload, __extra);
DM:  return ..()             (in a Destroy(force) override)         C#: return base.Destroy(force);
DM:  . = ..()                (in a setDir override, a void slot)    C#: await base.setDir(newdir);   (setDir is a DmTask slot at the snapshot)
```

A caller that uses the value of a `void` slot gets null: write the call as a statement and use `null`.

**Order at the top of a body.** A translated body starts with, in this order: the unpacking of extra parameters from `__extra` (7.2), the DM default prologue in declaration order (7.2), `object dot = null;` (7.4), `mob usr__caller = usr;` (10.8); then the DM statements. A 9.3 `try` or the 10.8 `try`/`finally` encloses all DM statements, the 9.3 one inside the 10.8 one.

### 7.5 `..()`

`..()` calls the parent definition, `base.name(...)`. With no arguments it passes **the current values** of all the current proc's arguments, extras included (BYOND ref ".. proc"; OpenDream `FromProcArguments`): `base.name(p1, p2, ...)` with the slot's parameters, and for a variadic slot either `__extra` itself (when this definition unpacked nothing from it) or `Dm.forward(__extra, x1, x2)` where `x1, x2` are the current values of the parameters this definition unpacked from `__extra` (the helper returns a copy of `__extra`, extended with nulls to at least as many entries as values given, whose first entries are replaced by them). With explicit arguments, `..(a, b)` passes those, converted to the slot's parameters (extras into `__extra`).

```
DM:  /obj/item/stack/use(used, transfer = FALSE, check = TRUE) ... . = ..()
C#:  dot = base.use(used, Dm.forward(__extra, transfer, check));
```

A type that defines the same proc twice: the later definition is `name`, the earlier one `name__d0` (non-virtual), and the later one's `..()` calls `name__d0` (`docs/ai/specs/move-chain.md` section 9, item 1). The same holds for a BYOND builtin proc that tg defines on the builtin type itself: `/datum/Topic(href, href_list[])` calls `..()` into BYOND's own `/datum/Topic` (`datums/datum.dm:105-106`), so BYOND's definition is the non-virtual `Topic__d0` on the engine part (`Builtin/datum.builtin.cs`), tg's is the slot's root `Topic`, and its `..()` is `Topic__d0(href, href_list);`.

### 7.6 `args`

A proc that reads the DM `args` var is in a variadic slot (7.2). It builds `args` once, as a local declared right before the **top-level** statement of the body that contains its first use (when that use is nested in an `if` or a loop, the declaration goes before the whole outer statement, so that later uses outside it are in scope: `/datum/callback/Invoke` first reads `args` inside `if(!usr)` at `datums/callback.dm:100` and again at `:108`, so `args` is declared before that `if`): `DmList args = Dm.args(<the slot's C# parameters in order>, <__extra, or Dm.forward(__extra, x1, ...) when this definition unpacked x1, ... from it>);`, the same argument rule as `..()` (7.5); `arglist(args)` passes it on. In DM `args` is a live view of the parameters (OpenDream `ProcArgsList`: writing `args[i]` writes the parameter, and a later parameter write shows in `args`); the port's list is a copy taken at that statement. tg's two `args` writers (`/atom/New`, `atoms_initializing_EXPENSIVE.dm:72`, and the `INITIALIZE_IMMEDIATE` macro, `__DEFINES/subsystems.dm:101`) write `args[1]`, pass the list on and never read the parameter again, so the difference is not observable at the snapshot; a translator who finds a proc that mixes parameter writes with reads of `args` reports it to the lead.

```
DM:  /atom/New(loc, ...)                                            (atoms_initializing_EXPENSIVE.dm:65; loc is unpacked from __extra, Appendix A)
     ...
     args[1] = do_initialize == INITIALIZATION_INNEW_MAPLOAD      (:72)
     if(SSatoms.InitAtom(src, FALSE, args))
C#:  DmList args = Dm.args(Dm.forward(__extra, loc));
     args[1] = Dm.num(do_initialize == INITIALIZATION_INNEW_MAPLOAD);
     if (Dm.truthy(SSatoms.InitAtom(this, FALSE, args)))
```

### 7.7 Dynamic calls and proc references

| DM | C# |
|---|---|
| `PROC_REF(x)`, `VERB_REF(x)` | `nameof(x)` (the C# method name; `x__proc` for a renamed proc; tg's macro is `nameof(.proc/x)`, text) |
| `TYPE_PROC_REF(/obj/item, x)`, `TYPE_VERB_REF(/obj/item, x)` | `nameof(obj_item.x)` |
| `GLOBAL_PROC_REF(x)` | `"/proc/" + nameof(GlobalProcs.x)`, the text constant `"/proc/x"` (tg's macro is the proc path `/proc/x`, `__byond_version_compat.dm:35`, not a name). The binder calls a name of the form `"/proc/x"` as the global proc whatever the receiver, so `CALLBACK(src, GLOBAL_PROC_REF(to_chat), owner, msg)` (`lost_crew_manager.dm:100`, `thanatorenasia.dm:56`) calls the global `to_chat` as in DM, where a bare `nameof` would look for `src.to_chat` |
| a bare proc path used as a value (`cmp=/proc/cmp_text_asc`, `__HELPERS/_lists.dm:733`; `sortTim(results, cmp=/proc/cmp_numeric_dsc, associative = TRUE)`, `structures/votingbox.dm:188`) | `"/proc/" + nameof(GlobalProcs.cmp_text_asc)`, exactly as `GLOBAL_PROC_REF(cmp_text_asc)`, whose expansion it is (its C# type is `string`, 3.2) |
| `GLOBAL_PROC` (the define) | `GLOBAL_PROC` (a text constant from `Defines`) |
| `call(obj, name)(a, b)` | `Dm.Call(obj, name, a, b)` |
| `call(obj, name)(arglist(L))` | `Dm.Call(obj, name, Dm.arglist(L))` (assoc entries with text keys become named arguments) |
| `call(GLOBAL_PROC, name)(...)`, `call(/proc/x)(...)` | `Dm.Call(null, name, ...)` (a null receiver means a global proc) |
| `hascall(obj, name)` | `hascall(obj, name)` |
| `CALLBACK(obj, PROC_REF(x), a)` (`new /datum/callback`) | `Dm.@new<datum_callback>(obj, nameof(x), a)`; a named argument is `Dm.named(...)` |
| `INVOKE_ASYNC(obj, PROC_REF(x), a)` | `x` resolves statically to a slot with a `DmTask` signature (`suspends = yes`, 7.1): `Waiting.Detach(obj.x(a))`; to any other slot (synchronous, also when it is coloured only through `waitfor = FALSE`): `Waiting.DetachSync(() => obj.x(a))` (always this form in translated code; inlining it at a hot site is a later optimization by the lead); `x` not static (a var holding a proc name): `Waiting.Detach(Dm.Call(obj, x, a))` (section 8) |

`Dm.Call` resolves the name on the runtime type (virtual dispatch; on the receiver `world`, a tg `/world` proc, the `DmWorld` extension method of 1.5, with `world` passed first), binds positional arguments (missing ones are null, extra ones go to `__extra` or are dropped), binds named arguments (7.3) and invokes. Because its target is not known statically, it always returns a `DmTask<object>` (already completed when the callee is synchronous or did not sleep), used with the call forms of section 8 exactly like a call of a `DmTask` slot: `object r = await Dm.Call(...)` in an async proc, `object r = Waiting.NoSleep(Dm.Call(...))` in a synchronous one, `Waiting.Detach(Dm.Call(...))` for a detached call. The same holds for tg's `/datum/callback/proc/Invoke` (`docs/ai/specs/waiting-model.md`). Signal handlers may declare fewer parameters than the signal sends; the binder pads and drops.

### 7.8 Global procs

Every tg `/proc/x` is `public static <ret> x(...)` in `public static partial class GlobalProcs` (namespace `Anomaly.Core.Tg`), in the mirror file of its `.dm`, with the same parameter and return rules (a global proc is its own slot with no overrides). Translated code calls it bare through `using static`. C# looks up type members before `using static` members, which matches DM (a proc of `src` shadows a global proc of the same name); DM's explicit `global.x()` / `::x()` is `GlobalProcs.x()`. If a field of the current class has the global proc's name (CS1955), the call is written `GlobalProcs.x(...)`.

```
DM:  /proc/qdel(datum/to_delete, force = FALSE)       (controllers/subsystem/garbage.dm:346; to_delete is object by 3.4: qdel type-tests it and logs a non-datum)
C#:  // src/Core/Tg/controllers/subsystem/garbage.cs
     public static partial class GlobalProcs
     {
         // tg: code/controllers/subsystem/garbage.dm:346
         public static void qdel(object to_delete = null, float? force = null)
```

### 7.9 Lint and annotation macros

Mapped to attributes with no runtime effect: `SHOULD_CALL_PARENT(TRUE)` -> `[ShouldCallParent]` (`SHOULD_CALL_PARENT(FALSE)`, which only lifts an inherited requirement for SpacemanDMM, is dropped: `datums/callback.dm:80`), `SHOULD_NOT_SLEEP` -> `[ShouldNotSleep]`, `SIGNAL_HANDLER` -> `[SignalHandler]` (the proc is synchronous, section 8), `SHOULD_NOT_OVERRIDE` -> `[ShouldNotOverride]`, `SHOULD_BE_PURE` -> `[ShouldBePure]`, `PROTECTED_PROC` / `PRIVATE_PROC` -> `[DmProtected]` / `[DmPrivate]`; methods stay `public virtual` (no C# access modifiers, no `sealed`). `RETURN_TYPE(path)` is used only by the slot registry (7.4) and otherwise dropped; the DM 516 return-type hint on a proc declaration (`/proc/sortTim(...) as /list`, `sorts/helpers.dm:56`; `/proc/find_record(value, locked_only = FALSE) as /datum/record`, `__HELPERS/_lists.dm:778`) is dropped like `RETURN_TYPE`; `UNLINT(x)` is `x`. A method's attributes form one list: `[DmParams(...)]` first, then the lint attributes in the order their macros appear in the DM body (`[DmParams("used", "transfer", "check"), ShouldCallParent]`). Other `set` statements: `set waitfor` is section 8, verb settings are 7.10, `set background` is dropped.

### 7.10 Verbs, operator overloads

Verbs (`/mob/verb/x`, `set name/category/src`) are translated with the player command layer (M9-M10); until then they are `Dm.NotTranslated`. (M4a exception, the lead's for D128: `/mob/verb/examinate`'s body is translated in M4a by P2 as the proc tg's own code also calls, `examinate(A)` from the click chain and the screen objects; its `set` statements, the verb panel and the right-click verb menu stay M9.) DM operator overloads (`proc/operator+`, `operator""`) are methods named `operator__add`, `operator__text` and so on, found by the shim's `Dm.add` / `Dm.str` when the operand is a datum (1 tg site: timer.dm's `operator""`).

---

## 8. Waiting (pointer to `docs/ai/specs/waiting-model.md`)

The waiting model, colouring and all call forms are defined in `docs/ai/specs/waiting-model.md`. One line per DM construct:

| DM | C# |
|---|---|
| a proc in a slot whose `slots.tsv` signature is `DmTask` / `DmTask<object>` (exactly the slots with `suspends = yes`, 7.1) | `async DmTask` / `async DmTask<T>` (also when this definition's own body never awaits); translators never decide colour themselves |
| a proc in a slot coloured only through `waitfor = FALSE` definitions (`colored = yes`, `suspends = no`) | the synchronous signature of `slots.tsv`; the definitions that may sleep get the wrapper of the `set waitfor = FALSE` row below |
| `sleep(x)` | `await sleep(x);` |
| `stoplag()`, `do_after(...)`, `tgui_*`, `UNTIL(c)` | `await stoplag();`, `await do_after(...)`, `while (!<c as a condition, 4.1>) await stoplag();` (`while (!Dm.truthy(target)) await stoplag();`, `while (!(state == DONE)) await stoplag();`) |
| a call of a `DmTask` slot inside an async proc | `await obj.foo(...)` / `object x = await obj.foo(...);` |
| a call of a `DmTask` slot inside a synchronous context (`SIGNAL_HANDLER`, `SHOULD_NOT_SLEEP`, any non-async proc) | `Waiting.NoSleep(obj.foo(...))` / `object x = Waiting.NoSleep(obj.foo(...));` |
| `INVOKE_ASYNC(...)`, `ASYNC` / `spawn(-1)` around a single call | `Waiting.Detach(obj.foo(...))`; for a synchronous `foo`, `Waiting.DetachSync(() => obj.foo(...))` (7.7); `_ = obj.foo();` is forbidden |
| `set waitfor = FALSE` (own or inherited from the definition it overrides) | the callee-side wrapper with a `DotBox` for `.` (waiting-model.md 5.4: a plain method in a synchronous slot, an `async` one that returns a completed task in a `DmTask` slot); callers call it like any proc |
| `spawn(d) { body }`, `ASYNC { body }` | `spawn(d, async () => { body });` (`ASYNC` is `spawn(-1)`), with the copies below |

The forms the waiting model forbids (a dropped or stored `DmTask`, a direct read of one, an `await` of anything but a kernel awaitable, an `await` in a `[SignalHandler]`/`[ShouldNotSleep]` proc; waiting-model.md section 5) are build errors ANOM0001 to ANOM0005 of the waiting-model analyzer (waiting-model.md 5.6).

**`spawn` copies locals.** DM copies the proc's locals and arguments into the spawned block, so neither side sees the other's later writes (BYOND ref "spawn proc"); a C# lambda shares them. Before `spawn`, each local and parameter that the block reads is copied into a fresh local `<name>__s`, and the block uses only the copies:

```
DM:  for(var/i in 1 to 3)
         spawn(10)
             world.log << "[i]"
C#:  for (float i = 1, i__end = 3; i <= i__end; i++)
     {
         float i__s = i;
         spawn(10, async () => { Dm.output_to(world.log, Dm.text("[]", i__s)); });
     }
```

`src` (`this`) and objects are shared, as in DM. `usr` is carried by the scheduler.

---

## 9. Runtime errors

**9.1 The rule.** In DM a runtime error (including `CRASH`, a failed `ASSERT`, `throw` without a matching `try`) ends only the failing proc, which returns its current `.` to its caller; the caller carries on (BYOND ref `throw`: "the current proc will end and control will return to the caller"). **The port deviates** (DV1, [C0]): a runtime error is a C# exception that unwinds through its callers to the nearest **isolation boundary**, where `World.ReportRuntime` logs it and execution continues after the boundary. Never describe the port's behaviour as DM semantics in docs or comments.

**Every exception is a DM runtime error**, for every purpose (boundaries, DM `try`/`catch`, the 9.3 pattern, the runtime count): a `DmRuntimeException` from the shim (`CRASH`, a type mismatch, a bad index), an `InvalidCastException` from a cast (3.5, 6.2: the C# shape of DM's "undefined variable" or "undefined proc" runtime on a value of the wrong type), a `NullReferenceException` from a member access on null. Boundaries catch `Exception`, and no translated code wraps one exception type into another.

**9.2 The isolation boundaries** (each catches, reports through `World.ReportRuntime` and continues; the authoritative list is `docs/ai/specs/waiting-model.md` section 9.2, repeated here):

- provided by the Kernel, nothing to write: each scheduler entry (a woken sleeper, a spawned block); each world phase; `Waiting.Detach`, `Waiting.DetachSync` and `spawn`; the detached remainder of a callee after `Waiting.NoSleep` or a `waitfor = FALSE` wrapper returned;
- written in translated code, at the call:
  1. the `fire()` call inside `/datum/controller/subsystem/proc/ignite` (`controllers/subsystem.dm:148`), so `ignite` continues with `. = state` exactly as in DM;
  2. each timer callback invocation in `SStimer`;
  3. each signal handler call inside `/datum/proc/_SendSignal` (`datums/signals.dm:113`): the next handler still runs and the failed one contributes nothing to the result;
  4. each `/datum/callback/proc/Invoke` and `InvokeAsync`: the call returns null;
  5. each per-item `process()` call in the processing subsystems and each `Life()` call in `SSmobs`;
  6. the `Initialize` call inside `/datum/controller/subsystem/atoms/proc/InitAtom`: InitAtom continues with a null result and records the bad init, as in DM.
  7. the `Destroy()` call inside `/proc/qdel` (`controllers/subsystem/garbage.dm:375`): qdel continues with a null hint (`no_hint`, `Queue`), as in DM.
  8. the `subsystem.Initialize()` call inside `/datum/controller/master/proc/init_subsystem` (`controllers/master.dm:501`): init_subsystem continues with a null result, reports the subsystem as initialized with errors, and the boot goes on, as in DM.
  9. each `LateInitialize()` call in the late-loader loop of `/datum/controller/subsystem/atoms/proc/InitializeAtoms` (`controllers/subsystem/atoms.dm:70`): the loop goes on with the next atom, then `late_loaders.Cut()` and the queued deletions run, as in DM.
- written in the engine parts (BYOND's own code: `src/Core/Tg/Builtin/` and the host seams of `src/Core/Tg/Infrastructure/`), where BYOND itself starts a proc: the engine's call of `client/New` for a connecting player (`ClientSeam.Connect`, M1): a runtime there ends the connection as a refusal (BYOND: `New` returned null), is reported, and is recorded for the host in `ClientConnection.Error`.

At a written boundary the translator writes exactly this, around the one call (here `var/result = A.Initialize(arglist(arguments))`, atoms_initializing_EXPENSIVE.dm:17, with the `arglist` form of 7.3):

```csharp
object result = null;
try { result = Waiting.NoSleep(Dm.Call(A, nameof(atom.Initialize), Dm.arglist(arguments))); }
catch (Exception e) { World.Current.ReportRuntime(e); result = null; } // isolation boundary (translation-rules.md 9.2)
```

**9.3 The `.`-on-runtime idiom.** tg procs that set `.` before work so that "if we runtime" the caller sees a failure value get a proc-level try/catch that returns `dot`, which reproduces DM for them. Known sites at the snapshot: `/proc/Recreate_MC` (`master.dm:256-257`, `. = -1`), `/datum/controller/master/proc/CheckQueue` (`master.dm:796-797`, `. = 0`), `/datum/controller/master/proc/RunQueue` (`master.dm:830-832`, "Returns 0 if runtimed"), `/proc/_stack_trace` (`__HELPERS/stack_trace.dm:3-4`: its `CRASH` exists to log "without ending the current proc", so the catch reports it and returns to the caller), `/datum/controller/master/proc/Loop` (`master.dm:589`, `. = -1`: `StartProcessing` reads -1 as "MC crashed or runtimed, restarting", `:579`), `/datum/controller/subsystem/atoms/proc/CreateAtoms` (`atoms.dm:57-59`: tg moved it out of `InitializeAtoms` so that a runtime while creating atoms "doesn't cause initialized to totally break"; it returns nothing, the catch only reports), `/datum/persistent_client/Destroy` (`modules/client/persistent_client.dm:49-50`: `. = QDEL_HINT_LETMELIVE` before its `CRASH`, so that qdel keeps the persistent client), `/datum/controller/subsystem/verb_manager/proc/queue_verb` (`controllers/subsystem/verb_manager.dm:137`, `. = FALSE //errored`): The same shape, a try/catch that reports and goes on, stands around the inner load call of `/datum/parsed_map/proc/_load_impl` (`reader.dm:326`): tg notes there that a runtime of the inner procs must still reach its cleanup, and without it `SSatoms` would stay in its map loading state (map editor ME3).

```csharp
public virtual object CheckQueue(DmList subsystemstocheck = null)
{
    object dot = null;
    try
    {
        dot = 0; // so the mc knows if we runtimed
        ...
    }
    catch (Exception e) { World.Current.ReportRuntime(e); return dot; } // DM: a runtime ends only this proc and returns '.'
}
```

A translator who finds another tg site whose comment or caller depends on DM's per-proc behaviour adds the same pattern with a `// DM: ...` comment and reports the site to the lead for this list.

**9.4 DM `try`/`catch`** (22 sites) becomes C# `try`/`catch (Exception ex)`. A catch variable is the DM exception datum built from the C# exception: `catch(var/exception/e)` -> `catch (Exception ex) { exception e = Dm.exception(ex); ...`. `throw x` -> `throw Dm.throw_value(x)`; `EXCEPTION(msg)` -> `Dm.EXCEPTION(msg, "code/x.dm", 123)`.

```
DM:  try
         load_data()
     catch(var/exception/e)
         log_world("load failed: [e]")
C#:  try
     {
         load_data();
     }
     catch (Exception ex)
     {
         exception e = Dm.exception(ex);
         log_world(Dm.text("load failed: []", e));
     }
```

**9.5 `CRASH`, `ASSERT`, `stack_trace` carry the DM location.** Runtime messages name the DM file and line, never the C# file (`[CallerFilePath]` is not used in translated code; only the test harness of ported tg unit tests uses it, 15). The translator fills the literals with what `__FILE__` and `__LINE__` give at that statement: the `.dm` path from `code/` and the DM line on which the statement (or the macro call) starts:

| DM | C# |
|---|---|
| `CRASH(msg)` | `throw Dm.CRASH(msg, "code/datums/datum.dm", 123);` |
| `ASSERT(cond)`, `ASSERT(cond, msg)` (tg's macro, `__DEFINES/assert.dm:4`) | expanded inline at the use (11.1): `if (!(<cond as a condition, 4.1>)) throw Dm.CRASH(assertion_message("code/x.dm", 123, "<cond as written in DM>", msg), "code/x.dm", 123);`, without the `msg` argument for `ASSERT(cond)`. The message is built only when the assertion fails, as in DM. For example `ASSERT(isnum(wait), "addtimer called with a non-numeric wait ([wait])")` at `code/controllers/subsystem/timer.dm:613` is `if (!(isnum(wait))) throw Dm.CRASH(assertion_message("code/controllers/subsystem/timer.dm", 613, "isnum(wait)", Dm.text("addtimer called with a non-numeric wait ([])", wait)), "code/controllers/subsystem/timer.dm", 613);`. `assertion_message` (`assert.dm:9`) is an ordinary global proc |
| `stack_trace(msg)` (tg's macro over `_stack_trace(message, __FILE__, __LINE__)`, `__DEFINES/stack_trace.dm:2`) | `stack_trace(msg, "code/x.dm", 123);` (`GlobalProcs`, with the use site's DM file and line as literals). The member reports `Dm.CRASH(msg, "code/x.dm", 123)` through `World.ReportRuntime` and **returns**: the runtime names the caller's location, which is what tg's `world/Error` makes of the `WORKAROUND_IDENTIFIER` text that `_stack_trace` crashes with (`modules/error_handler/error_handler.dm:62-66` moves the encoded file and line into the exception; that handler is not translated, so the member does the rewrite), and the caller continues, since tg's `_stack_trace` exists to log "without ending the current proc" (a thrown exception would unwind the caller, DV1). A direct call of `_stack_trace` has the 9.3 pattern |
| `__FILE__`, `__LINE__` | `"code/x.dm"`, `123` (the DM location) |
| `__PROC__` (a reference to the current proc; tg passes it to `call()`, `log_entry.dm:108`) | `nameof(<the current C# method>)`, like `PROC_REF` (7.7) |
| `__TYPE__` | `typeof(<class>)`; `null` in a global proc (BYOND ref) |
| `__IMPLIED_TYPE__` | section 10.4 |

---

## 10. Objects and types (details: `docs/ai/specs/object-model.md`)

**10.1 `new`.** `new /obj/item(loc, a)` -> `Dm.@new<obj_item>(loc, a)`; `new T(...)` with a `Type` value -> `Dm.@new(T, ...)` (returns `object`; convert by 3.5); implied type `var/obj/item/I = new(loc)` -> `obj_item I = Dm.@new<obj_item>(loc);` (the declared type of the assigned var, BYOND ref "new proc"; an implied `/list` without arguments, `var/list/x = new`, is `Dm.list()`, 6.1); `new /list(n)` -> `Dm.list_n(n)`; `new /list(a, b, ...)` and `var/L[a][b]` -> `Dm.@new<DmList>(a, b, ...)` (nested lists, 6.1). C# `new` is never used for a DM type. `Dm.@new` allocates, assigns the id, registers, runs the pre-New hook (var initializers that create objects; silent `loc` placement for atoms) and calls `New` through the binder (`docs/ai/specs/object-model.md` section 4).

**Creation without `New` is the engine's.** Where BYOND makes an object and sets vars before it calls `New` (a connecting player's `/client`: BYOND ref `/client/proc/New`, "the vars are set when New is called"), the engine part does the steps of `Dm.@new` itself and calls `New` afterwards: it allocates the world id (skipping the ids that `REF` reserves for lists, typepaths and `world`), registers the object, runs the pre-New hook, sets the engine's vars, then runs tg's `New` (`client.dm_engine_create`, called by `ClientSeam.Connect`). Only engine parts (`src/Core/Tg/Builtin/`) do this, with C# `new` of the class; translated code always creates with `Dm.@new`. Until the shim exposes the reserved-id test (SHIM-GAP: `DmRefs.IsReservedId` is internal), the engine part repeats it.

In DM the creating proc waits while `New` sleeps. Inside an async proc, a `new` whose `New` may sleep is awaited: `obj_x x = await Dm.new_async<obj_x>(loc);` / `object x = await Dm.new_async(T, ...);`. A `new` "may sleep" when the type's effective `New` (the definition on the type or on its nearest ancestor that defines `New`) is listed in `docs/ai/specs/generated/sleeping-new.tsv` with `suspends = yes` (column `definition` names the `New`), or when the type is computed (`new T` with a `Type` value). Every other `new`, and every `new` in a synchronous proc, is `Dm.@new`; if its `New` does sleep there, the no-sleep rule applies (DV5).

**10.2 Var defaults (generated from M3 on).** From M3 on (D111; the lead's M3 plan, written here by the M3 contracts step),
every DM type's var block comes from the types converter (`tools/Anomaly.Tools.Types`, `docs/ai/specs/types-converter.md`):
its fields, its constructor and its `dm_init_vars` are generated into `src/Core/Tg/Generated/Types/**` exactly by the rules
of this section, from OpenDream's folded values, together with `[DmPath]` when no hand-written part carries it and the
inert mark `[DmInert]` (D112).

- **Translators add procs, not var blocks.** A translator adds a type's procs in a `partial class` beside the generated
  part, in the mirror file of each `.dm` (1.1, 1.9), with the slot signatures of `slots.tsv`. It writes no field,
  constructor, `dm_tg_vars` or `dm_init_vars` for a type the generator covers, never edits a generated file, and reruns the
  tool after adding or removing procs or changing a translated proc's body (the inert marks and the tracker follow). A type is made live by
  `docs/ai/specs/types-converter.md` section 6 (its procs translated, or listed in `docs/ai/specs/live-partial.txt` with generated
  stubs), never by removing `[DmInert]` by hand.
- **Hand-written var blocks stay only for the classes that had one at the M3 start** (the 828 classes of M0-M2). The
  generator skips them and its conformance test checks every hand-written default against OpenDream's value; a mismatch is
  fixed in the hand-written file by that file's owner (a translation defect) or recorded as an OpenDream folding difference
  by the lead. New hand-written var blocks are not written.
- **Define names in data.** Generated defaults are OpenDream's folded constants (`layer = 2.9f` where tg writes
  `BELOW_OBJ_LAYER`); a lost define name is acceptable in generated data, while proc code keeps symbolic defines (11).
- What follows describes the shape both the generated code and the remaining hand-written blocks have.

(M2 integration: the engine part of a builtin type, `Builtin/**`, may also override `dm_init_vars` for its per-instance engine setup, calling the base first: `atom.overlays.builtin.cs` gives the instance's `overlays` and `underlays` their converter and owner there.)
- A var's **declaration** with a constant default is a field initializer in the declaring class: `public float force = 0;`, `public string icon_state = "x";`, `public Type spawn_type = typeof(obj_item_x);`.
- A subtype's **override** of a default (`force = 10` inside `/obj/item/crowbar`) is an assignment in the subtype's parameterless constructor, in DM declaration order: `public obj_item_crowbar() { force = 10; throwforce = 7; ... }`. The constructor lives in the part of the earliest file (`.dme` order) that overrides a var of the type and holds all of the type's overrides.
- A default that **creates an object or is not a constant** (`list(...)`, `new ...`, `typecacheof(...)`) is never a field initializer or constructor statement. It is an assignment in the type's override of the pre-New hook, after `base.dm_init_vars()`, in DM declaration order; and a constant override of a var that an ancestor initializes this way is also assigned there (after `base.dm_init_vars()`), like OpenDream's init proc:

```
DM:  /obj/item/bedsheet                                   (game/objects/structures/bedsheet_bin.dm:7-31)
         icon_state = "sheetwhite"
         var/list/dream_messages = list("white")
     /obj/item/bedsheet/blue                              (bedsheet_bin.dm:153-156)
         icon_state = "sheetblue"
         dream_messages = list("blue")
C#:  public partial class obj_item_bedsheet : obj_item
     {
         public DmList dream_messages;
         public obj_item_bedsheet() { icon_state = "sheetwhite"; }
         protected internal override void dm_init_vars() { base.dm_init_vars(); dream_messages = Dm.list("white"); }
     }
     public partial class obj_item_bedsheet_blue : obj_item_bedsheet
     {
         public obj_item_bedsheet_blue() { icon_state = "sheetblue"; }
         protected internal override void dm_init_vars() { base.dm_init_vars(); dream_messages = Dm.list("blue"); }
     }
```

A constant override (`dream_messages = null` in some subtype) of such a var goes into that subtype's `dm_init_vars` the same way, because the ancestor's initializer runs first. The ancestor's list is created and dropped, as in OpenDream (whose per-type init proc calls its parent's first); a `new` initializer in an ancestor therefore also runs for subtypes that override it.

**Builtin types.** BYOND's own defaults of a builtin type with an engine part (`/atom/movable`, `/obj`, `/mob`, `/turf`, `/area`) are set in the one parameterless constructor of that engine part (`Builtin/*.builtin.cs`). That constructor ends with `dm_tg_vars();`, declared beside it as `private partial void dm_tg_vars();`. tg's overrides of the type, which this rule would otherwise put in a constructor, go into `private partial void dm_tg_vars() { ... }` in the mirror part that would hold that constructor, in DM declaration order. A missing implementation is a compile error. So per type, BYOND's defaults come before tg's (OpenDream's order), and C#'s base-first constructor order keeps "the most derived override wins". `/atom` is the exception: its BYOND defaults are field initializers, so `_atom.cs` keeps a constructor. Subtypes of builtin types are ordinary classes. Open for a BYOND run: whether an override on `/atom` reaches a builtin subtype's own default (a bare `/turf`'s layer: 2 or 2.08). Nothing in M0 reads it.

C# constructors never touch the World (no ids, no registry, no `Dm.@new`), so the prototype that serves `initial()` can be built by the constructor alone.

**10.3 `initial()` and `::`.**

| DM | C# |
|---|---|
| `initial(x.var)` (`x` an instance: the runtime type's value) | `Dm.initial(x, (obj_item p) => p.var)` with `obj_item` the declared type of `x` |
| `initial(T.var)` (`T` a `Type`, V3) | `Dm.initial(T, (datum_wound p) => p.var)` with `datum_wound` the declared DM type of `T` |
| `initial(var)` (src's own var) | `Dm.initial(this, (<this class> p) => p.var)` |
| `/obj/item/x::var` (constant type: the compile-time value) | `Dm.initial(typeof(obj_item_x), (obj_item_x p) => p.var)` |
| `x::var` (`x` an instance or a `Type` var) | as `initial(x.var)` |
| `parent_type::var` in a var override | `Dm.initial(typeof(<parent class>), (<parent class> p) => p.var)` in the constructor |
| `A::B` with `B` a static var | the static accessor, section 10.6 |
| `T::parent_type`, `initial(T.parent_type)` (`T` a `Type` value) | `Dm.parent_type(T)` (`current_type = current_type::parent_type`, `datums/components/_component.dm:244`, is `current_type = Dm.parent_type(current_type);`) |
| `A::B()` (proc reference) | `nameof(<class of A>.B)` |
| `initial(x.var)` with an untyped `x` (`var/x`) | `Dm.initial(x, "var")` |

`Dm.initial` reads the cached prototype of the value's runtime type (a constructor-only instance); a var that is runtime-initialized (10.2) reads null there, as in DM.

**10.4 Type tests and searches.**

| DM | C# |
|---|---|
| `istype(x, /obj/item)` | `x is obj_item` |
| `istype(x)` (implied: the declared type of `x`) | `x is <declared class of x>` |
| `istype(x, T)` (`T` a `Type`) | `istype(x, T)` |
| `istype(x, /list)` | `x is DmList` |
| `ispath(T, /obj)`, `ispath(T)` | `ispath(T, typeof(obj))`, `ispath(T)` |
| `isnum(x)`, `istext(x)`, `islist(x)` | `isnum(x)`, `istext(x)`, `islist(x)` |
| tg's `is*` macros (`isobj(x)`, `isliving(x)`, `iscarbon(x)`, ...: `#define isliving(A) (istype(A, /mob/living))`) | `isliving(x)`: function-like macros are `GlobalProcs` members (11.1), returning `bool` |
| BYOND's own `is*` tests (`ismob(x)`, `isturf(x)`, `isarea(x)`, `ismovable(x)`, `isloc(x)`; tg does not `#define` them) | `ismob(x)` (`Builtins`, `bool`) |
| `astype(x, /obj/item)`, `astype(x)` (BYOND 516; the implied type is the assigned var's type, else `x`'s declared type) | `(x as obj_item)`, `(x as <implied class>)`; with a `Type` value `T`: `astype(x, T)` (returns `object`) |
| `__IMPLIED_TYPE__` (the type implied at that point: the assigned var's declared type) | `typeof(<that class>)` |
| `typesof(/obj)`, `subtypesof(/obj)` (tg) | `typesof(typeof(obj))`, `subtypesof(typeof(obj))` (a `DmList` of `Type`, in DM type-tree order) |
| `locate(/obj/item) in C`, `locate() in C` (implied type) | `Dm.locate<obj_item>(C)` (null `C`: nothing; an atom: its contents) |
| `locate(T) in C` with a typepath value `T` (a var or an argument) | `Dm.locate_type(T, C)` (`T` a `Type`; null `T` or `C`: nothing; returns `object`, convert by 3.5) |
| `locate(/obj/item)` (world) | `Dm.locate<obj_item>(world)` (it searches like a loop without `in`, 6.5: the first atom of an atom type, the first registered non-atom datum of `/datum` or a non-atom type, in id order) |
| `locate(x, y, z)` | `locate(x, y, z)` (`Builtins`) |
| `locate(ref_text)`, `locate(tag)` | `Dm.locate_ref(ref_text)` |
| `text2path(t)`, `"[T]"` | `text2path(t)`, `Dm.text("[]", T)` |

**10.5 qdel.** `qdel(x)`, `QDEL_NULL(x)`, `QDELETED(x)`, `QDELING(x)` and the `QDEL_*` macros are tg code, translated into `GlobalProcs` (`qdel(x)`, `QDEL_NULL(ref x)`, ...). `QDELING(X)` is `(X.gc_destroyed != null)` and `QDELETED(X)` is `(X == null || X.gc_destroyed != null)`: the port tests null where tg tests truthiness, so a datum qdel'd at world time 0 counts as deleted (DV2). `del(x)` (hard delete) is not translated (SSgarbage internals, section 13).

```
DM:  QDEL_NULL(beaker)              C#: QDEL_NULL(ref beaker);
DM:  if(QDELETED(src))              C#: if (QDELETED(this))
DM:  qdel(src)                      C#: qdel(this);
```

**10.6 Global and static state (frozen; shapes in `docs/ai/specs/object-model.md`).** No translated or shim class has a mutable C# `static` field. All DM global and static state lives in the per-world container `TgState` (one instance per `World`, reached through `TgState.Current`), and translated code reaches it through static accessor properties that keep the DM spelling: (M2 integration: a proc-level `var/static` that creates a datum is assigned in `TgState.InitStatics` like a type-level one, in compile order, even when a package translates its proc: the package reports it and the lead adds the line, since BYOND creates it at world start and a lazy creation moves the world ids; `/obj/item/organ/lungs`' `dummy` and `empty_breath` are there. The use may keep a `??=` creation for the test harness worlds that do not run `InitStatics` (it never fires in a booted world). A proc-level static whose initializer only registers metadata for the verb panel, `succumb`'s `____reg_whispered`, is left out with a comment, 7.10. A read of a subsystem that tg's Master always creates before `GLOB` and the map, `SSmapping` or `SSstation`, may be null-conditional, `SSmapping?.max_plane_offset`, where a test harness builds `GLOB` or turfs without it; a booted world always has it.)

| DM | C# declaration | C# use |
|---|---|---|
| `GLOBAL_LIST_EMPTY(player_list)` (and every `GLOBAL_*` var) | an instance field `public DmList player_list;` of `datum_controller_global_vars` plus `public void InitGlobalplayer_list()` | `GLOB.player_list` |
| `GLOBAL_REAL(GLOB, ...)`, `GLOBAL_REAL(Master, ...)`, `SUBSYSTEM_DEF(timer)` (`SStimer`), `GLOBAL_REAL_VAR(x)` | an `internal` field of `TgState` (`GLOB` and `Master`: the infrastructure part, below) | `public static ref datum_controller_subsystem_timer SStimer => ref TgState.Current.SStimer;` in `GlobalProcs` (likewise `Master`; `GLOB` is the exception below); use `SStimer.fire(...)`, `SStimer = this` |
| `/datum/component/var/static/list/foo` | `internal DmList datum_component__foo;` in a `TgState` part in the mirror file | `public static ref DmList foo => ref TgState.Current.datum_component__foo;` on `datum_component`; use `foo` inside the class, `datum_component.foo` elsewhere |
| proc-level `var/static/regex/r = new(...)` in `/datum/foo/proc/bar` | `internal regex datum_foo__bar__r = ...;` in a `TgState` part | `private static ref regex bar__r => ref TgState.Current.datum_foo__bar__r;`; the proc uses `bar__r` |
| `var/global/x` on a type | as `var/static` | as `var/static` |

**`GLOB` before the datum exists.** The `GLOBAL_*` vars are `var/global` vars of `/datum/controller/global_vars` (`GLOBAL_RAW`, `__DEFINES/__globals.dm:32`), so DM keeps them apart from the datum and a read or write through a null `GLOB` works: tg sets `GLOB.config_error_log` in `/world/Genesis` (`game/world.dm:91`) and reads `GLOB.sortInstance` in `Master/New` (`controllers/master.dm:110`), both before `master.dm:128` creates GLOB, and the values written then are still there after `/datum/controller/global_vars/New`. So the port has two accessors. `GLOB` (a plain getter, never null) is used only to reach a var: `GLOB.x`, `ref GLOB.x`. Before the datum exists it returns a per-world storage object of the GLOB type: made without its constructor, not a datum of the world, all vars null or 0, not hashed. `GLOB__var` (a get/set property) is the DM var itself, null until `GLOB = src`. Every use of GLOB as a value goes through it: `if(GLOB)`, `GLOB = src`, `RegisterSignal(GLOB, ...)`, `HAS_TRAIT(GLOB, ...)`, `COOLDOWN_START(GLOB, ...)`. Assigning a datum to `GLOB__var` moves every var of the storage that is not null or 0 into it and drops the storage. A `GLOBAL_*` var of C# type `float` therefore reads 0 before its `InitGlobal` runs, where DM reads null.

**The fields are `internal`.** Every field that a translated file adds to `TgState` in its own part (the storage of `SUBSYSTEM_DEF`, `GLOBAL_REAL` and `GLOBAL_REAL_VAR`, and the `<class>__<var>` / `<class>__<proc>__<var>` fields of translated statics) is `internal`; public access goes only through the `GlobalProcs` and class accessors above. So `AppendixCNameTests`, which requires every public `TgState` member to be named in Appendix C, needs no exemption for them. The infrastructure part (`src/Core/Tg/Infrastructure/TgState.cs`) keeps its public `GLOB` and `Master` fields, named in Appendix C, for hosts and tests.

Static initializers that do not create registered datums are `TgState` field initializers; one that does (`new /datum/...`) is assigned in `TgState.InitStatics()` in DM compile order. `GLOB`'s `InitGlobal*` procs run in DM compile order from `GLOB.Initialize` (object-model.md).

**`SUBSYSTEM_DEF`.** `SUBSYSTEM_DEF(X)` (`__DEFINES/MC.dm:93-97`; `PROCESSING_SUBSYSTEM_DEF` and the other variants alike) expands to `GLOBAL_REAL(SSX, /datum/controller/subsystem/X)`, a generated `/datum/controller/subsystem/X/New()` that runs `NEW_SS_GLOBAL(SSX)` and `PreInit()`, and the type block. The `GLOBAL_REAL` is the `TgState` field and accessor of the table; the generated `New` is translated in the subsystem's mirror file as the type's `New` override, with `NEW_SS_GLOBAL` (`MC.dm:29`, it reads `src`) expanded inline (11.1 (d)); the type block's var overrides go into the constructor (10.2):

```
DM:  SUBSYSTEM_DEF(garbage)                                  (controllers/subsystem/garbage.dm:24)
         name = "Garbage"
         ...
C#:  [DmPath("/datum/controller/subsystem/garbage")]
     public partial class datum_controller_subsystem_garbage : datum_controller_subsystem
     {
         public datum_controller_subsystem_garbage() { name = "Garbage"; ... }

         // The New() that SUBSYSTEM_DEF(garbage) defines (__DEFINES/MC.dm:93-96)
         public override async DmTask New(params object[] __extra)
         {
             if (SSgarbage != this)
             {
                 if (SSgarbage is datum_controller_subsystem_garbage)
                 {
                     Recover();
                     qdel(SSgarbage);
                 }
                 SSgarbage = this;
             }
             PreInit();
         }
     }
     public sealed partial class TgState { internal datum_controller_subsystem_garbage SSgarbage; }
     public static partial class GlobalProcs
     {
         public static ref datum_controller_subsystem_garbage SSgarbage => ref TgState.Current.SSgarbage;
     }
```

**10.7 Builtin vars of datums.** `type` is `GetType()` exposed as the property `type` on `datum`; `parent_type` is `Dm.parent_type(GetType())` (of a `Type` value `T`: `Dm.parent_type(T)`, 10.3); `tag` is a `string` property of the engine part (`docs/ai/specs/object-model.md` section 2): its setter keeps the world's tag index and makes the registry hold a tagged datum strongly; `vars` is not translated (`Dm.NotTranslated`, used by VV and Recover). `x.type == /obj/item` -> `x.type == typeof(obj_item)`.

```
DM:  if(istype(src, /obj/item/crowbar) && type != /obj/item/crowbar/red)
C#:  if (this is obj_item_crowbar && type != typeof(obj_item_crowbar_red))
```

**10.8 `src`, `usr`, `world`.** `src` is `this` (null in a global proc: `null`). `usr` is `usr`, a typed accessor in `GlobalProcs` (`mob`) over the Kernel's ambient `World.usr`, with DM's per-call semantics (`docs/ai/specs/waiting-model.md` section 10): each proc inherits `usr` from its caller, and whatever a callee assigns, the caller keeps its own. The Kernel restores it around every call of an `async` (`DmTask`) method and every scheduler entry, but it cannot see inside a synchronous method, so:

**Rule (usr save and restore).** A synchronous definition (its C# method is not `async`) that assigns `usr` anywhere in its body saves it on entry and restores it on every exit: `mob usr__caller = usr;` is the last line of the prologue (7.4) and a `try { ... } finally { usr = usr__caller; }` encloses all DM statements (around the 9.3 `try` when both apply), so a return, a fall-off and a runtime error all restore the caller's `usr`. An `async` method (a definition in a `DmTask` slot, or the `__body` of a waitfor wrapper) writes nothing: its `DmTask` builder saves and restores `usr` per call.

```
DM:  usr = M                       (M a mob, anywhere in the body of a synchronous proc)
C#:  mob usr__caller = usr;        (the last line of the prologue, 7.4)
     try
     {
         ...
         usr = M;
         ...
     }
     finally { usr = usr__caller; }
```

`world.time`, `world.tick_lag`, `world.fps`, `world.maxx/maxy/maxz`, `world.log`, `world.contents` are members of the shim's `world` facade over `World.Current` (`Dm.world`, class `DmWorld`: a class named `world` would make the bare name ambiguous; `world.time` is `float` deciseconds). `world.fps` and `world.tick_lag` are constants (20, 0.5): writes (`world.change_fps`) are not supported (DV6). There is no wall clock in the core (D31, DV3): `world.cpu` reads 0 and `world.tick_usage` reads the virtual usage of the tick (0 unless a subsystem charged work with `DmWorldState.ChargeTick`; SSair does, DV33), and `world.timeofday` and `world.realtime` advance with `world.time` from a fixed start value set by the host.

```
DM:  if(world.time < next_use)      C#: if (world.time < next_use)
DM:  usr.client.eye = src           C#: usr.client.eye = this;
```

**10.9 `image()` with a text second argument.** BYOND's `image(icon, loc, icon_state, layer, dir, pixel_x, pixel_y)` reads a text (or number) second argument as the icon_state and shifts every later positional argument down one place (`image(icon, "state", layer)`). `Dm.image` does the same shift for the arguments it was given by position, but a C# named argument binds to its parameter before the shift and so lands one place off. So when the second argument is text, the translation passes it, and every argument after it, by name; BYOND binds them the same way (loc stays null):

```
DM:  image('icons/effects/footprints.dmi', "[state]1", dir = print_dir)
C#:  image(Dm.resource("icons/effects/footprints.dmi"), icon_state: Dm.text("[]1", state), dir: print_dir)
DM:  image(icon, "lips_[style]", -BODY_LAYER, dir = d)
C#:  image(icon, icon_state: Dm.text("lips_[]", style), layer: -BODY_LAYER, dir: d)
```

A call without named arguments (`image(icon, "state")`, `image(icon, "state", layer)`) may stay positional.

---

## 11. Macros

**11.1 Expansion.** tg macros are expanded by hand at each use, in one of two forms, decided by the macro's definition alone:
- **Inline**, literally, with hygiene renames for its locals (2.6), when the macro (a) uses an argument as a name or member name (`COOLDOWN_START(cd_source, cd_index, cd_time)` assigns `cd_source.cd_index`), (b) stringifies an argument (`#x`) or pastes tokens into one identifier (`InitGlobal##X`), (c) contains control flow that leaves the macro or a declaration visible after it, (d) refers to the caller's context (`src`, `usr`, `.`, a caller's local: `MC_TICK_CHECK` reads `src.state`), (e) is one of the lazy-argument macros, whose expansion evaluates an argument only under a condition or once per iteration, so that a member receiving it evaluated once would change what it does: `SEND_SIGNAL` and `SEND_GLOBAL_SIGNAL` (their arguments are evaluated only when a listener exists), `LISTASSERTLEN(L, I, V...)` (`__HELPERS/_lists.dm:114`: `V` is evaluated once per new index, "so that list() can just make a new list for each item") and `ASSERT(condition, message...)` (`__DEFINES/assert.dm:4`: the message is built only when the condition fails; the form is in 9.5), or (f) uses `__FILE__`, `__LINE__`, `__PROC__` or `__TYPE__`, whose values belong to the use site and become the literals of 9.5 (`addtimer(args...)` is `_addtimer(args, file = __FILE__, line = __LINE__)`, `__DEFINES/subsystems.dm:218`, so `addtimer(cb, 5 SECONDS)` is `_addtimer(cb, 5 * SECONDS, file: "code/x.dm", line: 123)` with the C# named arguments of 7.3). Object-like macros that expand to a fragment (`CALLBACK` is `new /datum/callback`) are inline too.
- **A `GlobalProcs` member with the macro's name** otherwise, in the mirror file of the `.dm` that defines the macro; when the expansion is a call on `src` or on an explicit receiver (`AddComponent(...)` is `_AddComponent(list(...))` on `src`, `parent.AddComponent(...)` the same on `parent`: `AddElement`, `RemoveElement`, `AddComponent`, `AddComponentFrom`, `LoadComponent`, `__DEFINES/dcs/helpers.dm:14-26`), a non-virtual public instance method with the macro's name of the receiver's declared class (`datum`) in that mirror file (7.3). Each argument is evaluated once, which is what tg intends even where the macro text repeats an argument (`QDELETED(X)` reads `X` twice; `docs/ai/specs/m0-reading/defines.md` 6.2). An argument the macro assigns is passed by `ref` (its location is evaluated once): `QDEL_NULL(ref item)` (`qdel(item); item = null`), `LAZYADD(ref L, x)` (`L ||= list(); L += I`). A variadic macro takes `params object[]`; forwarding the variadic list (`list(##arguments)`, `_AddComponent(list(##arguments))`) is not pasting, and a named argument among the variadic ones is a `Dm.named` item that the member turns into what the expansion makes of it (a text-keyed list entry for `AddComponent`).
- **Defined exceptions**: `stack_trace` uses `__FILE__` and `__LINE__` but is a `GlobalProcs` call that takes the DM location as extra literal arguments (9.5). `ASSERT` is inline by (b), (e) and (f), in the form of 9.5. `INVOKE_ASYNC` and `UNTIL` have the forms of section 8.

**An expression macro used as a statement.** C# accepts only calls, assignments, increments, `await` and `new` as expression statements (CS0201). An inline expansion that is a conditional expression but stands alone as a DM statement is written as an `if`: `c ? x : y` becomes `if (c) x; else y;`, leaving out each branch that is a constant or a plain read (4.12); when only the false branch remains, the condition is negated by removing its leading `!` (`!c ? NONE : call` gives `if (c) call;`), or by wrapping it in `!( ... )` when it has none.

```
DM:  #define SEND_SIGNAL(target, sigtype, arguments...) ( !target._listen_lookup?[sigtype] ? NONE : target._SendSignal(sigtype, list(target, ##arguments)) )
DM:  if(SEND_SIGNAL(src, COMSIG_MOVABLE_PRE_MOVE, newloc) & COMPONENT_MOVABLE_BLOCK_PRE_MOVE)         (atoms_movable.dm:730, a value)
C#:  if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_PRE_MOVE]) ? NONE : this._SendSignal(COMSIG_MOVABLE_PRE_MOVE, Dm.list(this, newloc))), COMPONENT_MOVABLE_BLOCK_PRE_MOVE)))
DM:  SEND_SIGNAL(src, COMSIG_MOVABLE_MOVED, old_loc, movement_dir, forced, old_locs, momentum_change)  (atoms_movable.dm:902, a statement)
C#:  if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_MOVED]))
         this._SendSignal(COMSIG_MOVABLE_MOVED, Dm.list(this, old_loc, movement_dir, forced, old_locs, momentum_change));
```

**The signature of a macro member** is fixed by its definition, like a slot's by its root (one translator writes it, every caller converts to it by 3.5). A parameter is `float` when every use of it in the expansion is numeric (an operand of arithmetic or of a comparison with a number, an argument of a numeric builtin, 4.6, or the right side of an assignment to a `float` var or property: `LAZYSETLEN(L, V)`'s `L.len = V`, `__HELPERS/_lists.dm:72`; `SORT_TIM(to_sort, associative)`'s `sorter.associative = associative`, `sorts/helpers.dm:9`), otherwise `object`. An argument the macro assigns is a `ref` parameter of the assigned var's C# type; a macro used on vars of several C# types gets one overload per type, or a generic `ref T` parameter when the body needs nothing beyond `T : class` (`QDEL_NULL<T>(ref T item) where T : class`). The return type is `void` when the expansion is a statement, `bool` when it is a condition (a comparison, `in`, a type test, a null test, `!`, `&&`/`||` of conditions, or `(c ? TRUE : FALSE)` for any `c`: `QDELETED`, `isliving`, `TICK_CHECK`, and `HAS_TRAIT(target, trait)`, `(target?._status_traits?[trait] ? TRUE : FALSE)`, `__DEFINES/traits/_traits.dm:116`), and otherwise the C# type of the translated expansion (`CEILING(float x, float y)` gives `float`, `LAZYACCESS` gives `object`). A `bool` member is used directly as a condition and wrapped in `Dm.num` where its value is used (4.1): `if(HAS_TRAIT(src, TRAIT_X))` is `if (HAS_TRAIT(this, TRAIT_X))`, `return HAS_TRAIT(src, TRAIT_X)` is `return Dm.num(HAS_TRAIT(this, TRAIT_X));`. A member access on an `object` parameter casts to the type that declares the member, `((datum)X).gc_destroyed`.

**A macro of a file no package owns** (`__DEFINES/mobs.dm`, `__DEFINES/status_effects.dm`, `__HELPERS/mobs.dm`, `__HELPERS/_planes.dm`, `__HELPERS/visual_effects.dm`, ...) is still a member in the mirror of its file, never a private copy in its user's class nor an inline expansion of a member macro: a package that needs one adds the member to that mirror (creating it as a PARTIAL file when it does not exist) and lists the shared file in its result, or reports the macro to the lead (M2 integration: `ISADVANCEDTOOLUSER`, `COMPARE_LAYERS`, `IS_DEAD_OR_FAKING`, `DO_FLOATING_ANIM` and `STOP_FLOATING_ANIM` moved to their mirrors).

The datum/signals pilot may add named helpers for the hottest inline macros; they are added to Appendix C and then become the only form.

**11.2 Postfix time units.** `SECONDS`, `MINUTES`, `HOURS`, `DECISECONDS`, `TICKS`, `KILO`, `NEWTONS` and the other unit macros are textual postfix multiplications (`#define SECONDS *10`). They are emitted as constants (`SECONDS = 10`, `MINUTES = 600`) and every `<operand> UNIT` is rewritten as `<operand> * UNIT` **at the same place in the expression, adding no parentheses**. `TICKS` (`#define TICKS *world.tick_lag`, `__DEFINES/time.dm:78`) is not a constant and is not emitted: `<operand> TICKS` is rewritten as its expansion, `<operand> * world.tick_lag`, by the same rule (`world.tick_lag` is the facade's constant 0.5, DV6). `*` and `/` share precedence and associate left in both languages, so the C# means what DM means, including unparenthesised divisions, which tg has and which must not be "fixed":

```
DM:  round(1 + (H.seconds_in_low_pressure / 80 SECONDS), 0.05)     (_species.dm:1339; DM: (x / 80) * 10)
C#:  round(1 + (H.seconds_in_low_pressure / 80 * SECONDS), 0.05f)
DM:  addtimer(CALLBACK(src, PROC_REF(fall_off_wall)), 2.5 SECONDS)        (game/objects/effects/posters/contraband.dm:677; addtimer is inline, 11.1 (f))
C#:  _addtimer(Dm.@new<datum_callback>(this, nameof(fall_off_wall)), 2.5f * SECONDS, file: "code/game/objects/effects/posters/contraband.dm", line: 677);
```

**11.3 Other common macros.**

| DM | C# |
|---|---|
| `CALLBACK(...)` | `Dm.@new<datum_callback>(...)` (7.7) |
| `INVOKE_ASYNC(...)`, `ASYNC` | section 8 |
| `UNTIL(c)` (`__DEFINES/_helpers.dm:31`) | `while (!<c as a condition, 4.1>) await stoplag();` (section 8) |
| `CHECK_TICK`, `TICK_CHECK` (`__DEFINES/_tick.dm:19-21`) | `CHECK_TICK();` (a statement) and `TICK_CHECK()` (a `bool`), both `GlobalProcs`: `TICK_CHECK` is false (DV3; DV33 keeps it so while SSair's charged usage drives `MC_TICK_CHECK` and the Master), so `CHECK_TICK` never reaches its `stoplag()` and the proc stays synchronous |
| `MC_TICK_CHECK` | inline, literally (it also tests `state != SS_RUNNING` and therefore trips after a sleeping `fire()`) |
| `get_turf(A)`, `get_area(A)` | `get_turf(A)`, `get_area(A)` (`GlobalProcs`) |
| `NAMEOF(datum, X)`, `NAMEOF_STATIC` | `nameof(X)` |
| `REF(x)` | `REF(x)` (`GlobalProcs`, tg's macro over `text_ref`; the text form of a ref is `"[0x<hex dm_id>]"`) |
| `GLOB.x` | `GLOB.x` (10.6) |
| `GLOB` as a value (`if(GLOB)`, `GLOB = src`) | `GLOB__var` (10.6) |

**11.4 File-local `#define`s.** The defines converter emits the `#define`s of `code/__DEFINES` and the few files the `.dme` includes before them (`docs/ai/specs/defines-converter.md` section 2). A `#define` elsewhere in `code/` that its file `#undef`s again at its end is file-local, and is not emitted. Its translation lives in the mirror file: an object-like constant is a `private const` of the class whose code uses it (the type's class, or the file's `GlobalProcs` part for global procs), with the C# type the defines converter would give the same value (`int` for an integer within 2^24 and for bit flags, `float` otherwise, `string` for text), and a `// tg:` pointer to the `#define`; a function-like one follows 11.1, inline or as a `private` member of that class. A file-local define that code of two classes of the file uses is an `internal` member of the class of the file's main type (the subsystem in `controllers/subsystem/timer.dm`: `BUCKET_LEN`, `BUCKET_POS` and `TIMER_MAX`, which `/datum/timedevent` also uses), and the other class names it through that class. A `#define` outside `code/__DEFINES` that its file never `#undef`s is global, and the converter does not emit it either: it is a `public const` (or a 11.1 member) of the `GlobalProcs` part of its file's mirror, `UPDATE_TRANSFORM_ANIMATION_TIME` in `__HELPERS/visual_effects.cs`. When a `.dm`'s mirror is split into partial files, a file-local define goes into the partial whose code uses it: the `private const` belongs to the class, so every partial of the class sees it (`LIMITER_SIZE` to `ADMINSWARNED_AT` in `client_procs.click.cs`) (M2 integration).

```
DM:  #define MIN_MERGE 32                                (sorts/sort_instance.dm:6, #undef at :651)
     #define MIN_GALLOP 7                                (:9, #undef at :650)
C#:  public partial class datum_sort_instance : datum
     {
         // tg: code/__HELPERS/sorts/sort_instance.dm:6  #define MIN_MERGE 32 (file-local, #undef at :651)
         private const int MIN_MERGE = 32;
         // tg: code/__HELPERS/sorts/sort_instance.dm:9  #define MIN_GALLOP 7 (file-local, #undef at :650)
         private const int MIN_GALLOP = 7;
```

---

## 12. Engine services and presentation (details: `docs/ai/specs/presentation-seam.md`)

tg's message, sound and appearance **logic** is translated literally (who hears, falloff, pressure, `update_appearance`, overlays lists). Only BYOND's output primitives become world-owned sinks with no-op defaults in headless tests:

| DM | C# |
|---|---|
| `to_chat`, `visible_message`, `audible_message`, `playsound`, `playsound_local`, `update_appearance`, `set_light*` | translated tg procs |
| `SSchat.queue(target, message)`, `SSchat.send_immediate(...)` (the tgchat transport) | the message sink (presentation-seam.md) |
| `A << B` (`DIRECT_OUTPUT`, `SEND_SOUND`, `WRITE_FILE`) | `Dm.output_to(A, B)` (routes text to the message sink or the host log of `world.log`, sounds to the sound sink; a file target, `WRITE_FILE`, waits for the file seam of Appendix C; not named `output`, which stays free for BYOND's `output(msg, control)`) |
| `sound(file, repeat, wait, channel, volume)` | `sound(...)` (returns the builtin `sound` data class) |
| `animate(...)`, `flick(...)` | `animate(...)`, `flick(...)` (appearance sink events) |
| `browse`, `output(msg, control)` (`A << output(...)`), `winset`, `winget`, tgui | UI seam (later milestones; `Dm.NotTranslated` in M0). tgui itself is translated since M5 slice 3 (D148, DV44): SStgui, `/datum/tgui`, the ui states and the src_object procs are tg's; only `tgui_window`'s browser output is the seam (`client.dm_tgui_output`, protocol 16), and each window's TSX is translated into the client's C# kit (`godot/scripts/Shared/Tgui/Interfaces/`, `// tg: tgui/packages/tgui/interfaces/X.tsx:line`) |
| `alert(...)`, `input(...)` | `await alert(...)`, `await input(...)` (shim builtins that sleep in BYOND; stubs that complete with null until the UI milestone, Appendix C, waiting-model.md section 6) |

## 13. What is deliberately not translated

Presentation internals (icon rendering, overlay compiling in SSoverlays, huds, maptext layout, `client.images`, screen objects), tgui's browser transport (D148: tgui's DM side and its windows are translated, section 12) and browse UIs, SQL, rust-g and TGS wrappers, admin verbs, `Topic`/`href` flows (but tg's chat links: D154 makes a click on one a typed command that reaches the `src` datum's `Topic`, translated where such a link reaches it; protocol.md section 33), the lighting module's objects and presentation (`modules/lighting`: D24, the client computes the light it draws from replicated source state; the module itself is translated for gameplay, D170: SSlighting, the light sources, the corners and the turf's lumcount procs run on the server, and the lighting objects are not created, DV85), the garbage collector internals (SSgarbage queues, hard delete, reference tracking), `Failsafe` (a skeleton whose `New` only registers it as `Failsafe`, with `processing_interval` 0, so that the Master's `Loop` never recreates it: DV10), `Recover`, profiling and stat panels, `vars`/`global.vars` reflection (`GLOB`'s self-check is DV9; the `global.vars` scan of `Master/New` is `global_vars__subsystems`, `src/Core/Tg/Infrastructure/TgState.Subsystems.cs`). They get a seam, a stub, `Dm.Deferred` (1.4) or `Dm.NotTranslated`; `docs/ai/specs/not-translated.md` lists the deferred leaves and the seams as translation proceeds. Code under `UNIT_TESTS` (`#if defined(UNIT_TESTS)`, `#ifdef UNIT_TESTS`) is never translated, since the port builds without it, and `PERFORM_ALL_TESTS(x)` is `FALSE` (1.7); tg's unit tests themselves are ported as tests (15).

## 14. Deviations

A deviation from tg that changes core logic or mechanics gets a line in `docs/ai/DEVIATIONS.md` (id, what, why, decision) and a `// DEVIATION(DVn): ...` comment at the code. Plain C# adaptations made by these rules and optimizations without behaviour change do not. The registry is kept light (D86).

## 15. Tests

Every translated file lands with tests that run in seconds under `dotnet test`: shim semantics, behaviour tests written from the DM code (not from the C#), and an adapted port of the tg unit test when one exists (`code/modules/unit_tests`). Order-of-events tests use a recording harness (a list of strings appended by signal handlers or by recording presentation sinks) and assert the exact tg order. Determinism: the same scenario twice gives the same `World.ComputeStateHash()`. Code that involves async state machines is also built and tested in Release (`dotnet build -c Release`), where state machines are structs. Tests compare DM values with `Dm.eq` or `Dm.num` (a boxed `int` and the `float` of the same value are one DM number, 3.1), never with `object.Equals` or `Assert.Equal` on boxed values.

**Ported tg unit tests** (`code/modules/unit_tests`, which tg compiles only with `UNIT_TESTS`, so `/datum/unit_test` is not translated into `src/Core/Tg`) run on the test-side harness `TgUnitTest` (`tests/Anomaly.Core.Tests/Tg/UnitTests/TgUnitTest.cs`, tg's `/datum/unit_test` and `/proc/RunUnitTest` with tg's names; each concrete subclass is one xunit test in a fresh `World`):

- **Placement and names.** One file per tg test file in `tests/Anomaly.Core.Tests/Tg/UnitTests/`, named like the `.dm` (`dcs_get_id_from_elements.cs`). The class is named from tg's test path by 2.3, `/datum/unit_test/component_duping` -> `public sealed class datum_unit_test_component_duping : TgUnitTest` (sealed unless the port's own checks derive from it), with a `// tg: code/modules/unit_tests/<file>.dm:<line> (/datum/unit_test/<name>)` pointer; `/Run()` is `public override async DmTask Run()`. A tester package (one that tests translations it does not own: M2 T1 and T2) may keep the tg tests it ports in its own folder, several `.dm` files' tests in one file (`Tg/M2Medical/T1/PortedTgTests.cs`: `stuns`, `oxyloss_suffocation`, `full_heal`, `status_effect_validity`, `spawn_humans`; T2's `combat_help` is a plain xunit test in `Tg/M2Locomotion/T2/CombatHelpTests.cs`), so that parallel packages never write to `UnitTests/`; the classes keep the names, the `// tg:` pointers and the harness of this section, and the package that later owns the tested `.dm` moves them into `UnitTests/`, one file per `.dm` (M2 integration).
- **Constants and location.** The unit-test `#define`s (`UNIT_TEST_PASSED`, `UNIT_TEST_FAILED`, `UNIT_TEST_SKIPPED`, `TEST_PRE`, `TEST_DEFAULT`, `TEST_LONGER`, the `UNIT_TEST_*` flags, ...) are inside `#if defined(UNIT_TESTS)`, so the defines converter does not emit them: they are constants on `TgUnitTest`. The assertion macros' `__FILE__` and `__LINE__` come from `[CallerFilePath]` and `[CallerLineNumber]` parameters of the harness members, so a test writes `TEST_ASSERT(cond, reason)` with no location literals (the only place where the port names a C# file instead of a DM one, 9.5).
- **Failing.** `TEST_ASSERT*` and `TEST_FAIL` record the failure as tg's `Fail(reason, file, line)` does, and a failed `TEST_ASSERT*` throws `TestAssertionReturn`, which ends `Run` as tg's `return Fail(...)` does; the runner catches it.
- **`src` and statics.** The harness is not a datum and has no world id, so where tg's test uses `src` as a datum (`REF(src)`, a signal target) an allocated stand-in datum takes its place, `datum src = allocate<datum>();`, marked `// ADAPTED:`, until `unit_test.dm` is translated. A proc-level `var/static` of a test lives in a per-world extension of the test's own (`World.GetExtension`, an accessor `<proc>__<var>` as in 10.6), because `TgState` is in the Tg assembly.
- **`parent_type::v`** in a test type's var override is written as the constant expression it reads (the harness is no DM type for `Dm.initial`), with a comment naming the value's source: `test_flags = Dm.bitand(UNIT_TEST_BASIC, Dm.bitnot(UNIT_TEST_DEBUG_MAP_ONLY));` for `parent_type::test_flags & ~UNIT_TEST_DEBUG_MAP_ONLY` (`dcs_check_list_arguments.dm:28`).
- **Mock and fixture types.** A mock type that tg's test declares keeps its tg path and its 2.3 class name (`/datum/element/dcs_get_id_from_arguments_mock_element`). A fixture type the port invents for a test has a DM path `/datum/<kind>/test_<package>_<name>` (`/datum/component/test_dcs_unique`, `/datum/element/test_dcs_list_args`; a plain datum `/datum/test_<package>_<name>`, `/datum/test_dcs_host`), which no tg path starts with. Fixtures may share overrides through a C# base class without `[DmPath]` (tests only: it is no DM type, so `typesof` and the type registry never see it).

---

## Appendix A. Slot registry (M0 base slots)

**`docs/ai/specs/generated/slots.tsv` is authoritative; this appendix is an excerpt of it** (7.1): the C# signatures of the slots the M0 translation touches, generated by `tools/coloring/slots.py` from every definition in the tg tree (snapshot `1d99bd7f`) by sections 3.3, 7.2 and 7.4, copied here with the generated values. Besides the excerpt, a row records a per-slot decision the generator does not make (a narrowed return type, 7.4, or a parameter type decided from the `undecided` column, 3.3, or retyped by a "-" cell of 3.5); such rows say "decided" (the pilot's, at v1.2). The generated "Appendix A cross-check" in `docs/ai/specs/colored-procs.md` lists every row that differs from `slots.tsv` (at v1.2 the three decided parameter types of `GetComponent`, `GetExactComponent` and `add_to_arguments_that_are_lists`; W3.0 added the narrowed return type of `update_midnight_rollover`). "Extra-param overrides" counts the overrides that declare more parameters than the root (they make the slot variadic). Signatures are written without `DmTask`: when `slots.tsv` has `suspends = yes` for the slot (its `signature` column shows the final form), `void` becomes `DmTask` and `object` becomes `DmTask<object>` for the root and every override (at the snapshot, for example, `/datum` `New`, `/datum/component` `Initialize` and the Move-chain slots); a slot coloured only through `waitfor = FALSE` definitions (`colored = yes`, `suspends = no`: `/datum` `process`, `/atom` `LateInitialize`, `/atom` `Bumped`) keeps the synchronous form written here, and its definitions that may sleep get the waitfor wrapper (7.1; waiting-model.md 4.2 and 5.4). "waitfor" is the root's own `set waitfor` and, in brackets, overrides that set it themselves; `colored-slots.tsv` (column `waitfor_false`) is authoritative. A root's translator who decides one of the parameters that `slots.tsv` lists as undecided (below the table), retypes a parameter by 3.5 or narrows a return type changes the row here in the same commit and reports it to the lead (7.1).

| Slot (root) | DM root (tg location) | C# signature (root; overrides identical) | Extra-param overrides | waitfor |
|---|---|---|---|---|
| `/datum` `New` | BYOND builtin | `public virtual void New(params object[] __extra)` | 704 (every `New` override that declares parameters, including the `New(loc, ...)` that `INITIALIZE_IMMEDIATE` generates) | |
| `/datum` `Destroy` | `Destroy(force = FALSE)` datums/datum.dm:125 | `public virtual object Destroy(float? force = null)` | 0 | |
| `/datum` `process` | `process(seconds_per_tick)` controllers/subsystem/processing/processing.dm:48 | `public virtual object process(object seconds_per_tick = null, params object[] __extra)` | 2 | FALSE (1 override: FALSE) |
| `/atom` `New` | `New(loc, ...)` game/atom/atoms_initializing_EXPENSIVE.dm:65 | override of `/datum` `New`: `[DmParams("loc")] public override void New(params object[] __extra)` | | |
| `/atom` `Initialize` | `Initialize(mapload, ...)` atoms_initializing_EXPENSIVE.dm:115 | `public virtual object Initialize(object mapload = null, params object[] __extra)` | 633 | |
| `/atom` `LateInitialize` | `LateInitialize()` atoms_initializing_EXPENSIVE.dm:172 | `public virtual void LateInitialize(params object[] __extra)` | 1 | FALSE |
| `/atom` `Entered` | `Entered(atom/movable/arrived, atom/old_loc, list/atom/old_locs)` game/atom/_atom.dm:620 | `public virtual object Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)` | 0 | (1 override, `/area`: FALSE) |
| `/atom` `Exited` | `Exited(atom/movable/gone, direction)` _atom.dm:643 | `public virtual object Exited(atom_movable gone = null, object direction = null)` | 0 | |
| `/atom` `Exit` | `Exit(atom/movable/leaving, direction)` _atom.dm:629 | `public virtual object Exit(atom_movable leaving = null, object direction = null)` | 0 | |
| `/atom` `Enter` | BYOND builtin `Enter(atom/movable/O, atom/oldloc)` (parameter names from the BYOND reference); tg overrides it on `/turf` (`Enter(atom/movable/mover)`, turfs/turf.dm:434) and `/turf/open/openspace` (`Enter(atom/movable/movable, atom/oldloc)`, openspace.dm:49), both with `[DmParams]` | `public virtual object Enter(atom_movable O = null, atom oldloc = null)` (builtin body: move-chain.md) | 0 | |
| `/atom` `Bumped` | `Bumped(atom/movable/bumped_atom)` _atom.dm:407 | `public virtual object Bumped(atom_movable bumped_atom = null)` | 0 | FALSE |
| `/atom` `CanPass` | `CanPass(atom/movable/mover, border_dir)` _atom.dm:225 | `public virtual object CanPass(atom_movable mover = null, object border_dir = null)` | 0 | |
| `/atom` `CanAllowThrough` | `CanAllowThrough(atom/movable/mover, border_dir)` _atom.dm:243 | `public virtual object CanAllowThrough(atom_movable mover = null, object border_dir = null)` | 0 | |
| `/atom` `setDir` | `setDir(newdir)` _atom.dm:579 | `public virtual void setDir(object newdir = null)` | 0 | |
| `/atom/movable` `Move` | `Move(atom/newloc, direction, glide_size_override = 0, update_dir = TRUE)` game/atoms_movable.dm:687 (defined twice: `Move__d0`) | `public virtual object Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)` | 0 | |
| `/atom/movable` `Moved` | `Moved(atom/old_loc, movement_dir, forced, list/old_locs, momentum_change = TRUE)` atoms_movable.dm:896 | `public virtual object Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)` | 0 | |
| `/atom/movable` `forceMove` | `forceMove(atom/destination)` atoms_movable.dm:1159 | `public virtual object forceMove(atom destination = null, params object[] __extra)` | 2 | |
| `/atom/movable` `doMove` | `doMove(atom/destination)` atoms_movable.dm:1169 | `public virtual object doMove(atom destination = null)` | 0 | |
| `/atom/movable` `abstract_move` | `abstract_move(atom/new_loc)` atoms_movable.dm:676 | `public virtual object abstract_move(atom new_loc = null)` | 0 | |
| `/atom/movable` `Bump` | `Bump(atom/bumped_atom)` atoms_movable.dm:980 | `public virtual object Bump(atom bumped_atom = null)` | 0 | |
| `/datum/controller` `Initialize` | `Initialize()` controllers/controller.dm:6 | `public virtual object Initialize(params object[] __extra)` (master's `Initialize(delay, init_sss, tgs_prime)` adds params) | 1 | (2 overrides, master and failsafe: FALSE) |
| `/datum/controller/subsystem` `fire` | `fire(resumed = FALSE)` controllers/subsystem.dm:163 | `public virtual void fire(float? resumed = null, params object[] __extra)` | 1 | (1 override: FALSE) |
| `/mob/living` `Life` | `Life(seconds_per_tick = SSMOBS_DT)` modules/mob/living/life.dm:12 | `public virtual object Life(object seconds_per_tick = null, params object[] __extra)` (P5: the default `SSMOBS_DT` is not a constant, so the parameter keeps the P2 type and the prologue assigns the default) | 1 | FALSE |
| `/datum/component` `Initialize` | `Initialize(...)` datums/components/_component.dm:68 | `public virtual object Initialize(params object[] __extra)` | 326 | |
| `/datum/element` `Attach` | `Attach(datum/target)` datums/elements/_element.dm:29 | `public virtual object Attach(datum target = null, params object[] __extra)` | 159 | |
| `/datum/element` `Detach` | `Detach(datum/source, ...)` _element.dm:42 | `public virtual object Detach(datum source = null, params object[] __extra)` (decided: `source` keeps `datum`; it is the datum the element leaves, and `initial()` and `::` read that instance) | 3 | |
| `/obj/item` `use` | `use(used)` game/objects/items.dm:1274 | `public virtual object use(object used = null, params object[] __extra)` | 8 | |
| `/datum` `GetComponent` | `GetComponent(datum/component/c_type)` datums/components/_component.dm:255 | `public virtual object GetComponent(Type c_type = null)` (decided: `c_type` is the typepath of the component sought, read with `initial(c_type.dupe_mode)`; `slots.tsv` keeps the declared type as undecided) | 0 | |
| `/datum` `GetExactComponent` | `GetExactComponent(datum/component/c_type)` _component.dm:275 | `public virtual object GetExactComponent(Type c_type = null)` (decided, as `GetComponent`) | 0 | |
| `/datum/controller/subsystem/processing/dcs` `add_to_arguments_that_are_lists` | `add_to_arguments_that_are_lists(list/argument, datum/element/element_type)` controllers/subsystem/dcs.dm:96 | `public virtual void add_to_arguments_that_are_lists(DmList argument = null, Type element_type = null)` (decided: `element_type` is the element's typepath that `GetIdFromArguments` passes, read with `initial()`) | 0 | |
| `/proc` `sortTim` | `sortTim(list/to_sort, cmp = GLOBAL_PROC_REF(cmp_numeric_asc), associative = FALSE, fromIndex = 1, toIndex = 0) as /list` __HELPERS/sorts/helpers.dm:56 | `public static object sortTim(DmList to_sort = null, string cmp = null, float? associative = null, float? fromIndex = null, float? toIndex = null)` (`cmp` is a proc reference: text, 3.2) | 0 | |
| `/proc` `sortMerge` | `sortMerge(...)` with the same parameters, sorts/helpers.dm:71 | `public static object sortMerge(DmList to_sort = null, string cmp = null, float? associative = null, float? fromIndex = null, float? toIndex = null)` | 0 | |
| `/proc` `sortInsert` | `sortInsert(...)` with the same parameters, sorts/helpers.dm:87 | `public static object sortInsert(DmList to_sort = null, string cmp = null, float? associative = null, float? fromIndex = null, float? toIndex = null)` | 0 | |
| `/proc` `sort_list` | `sort_list(list/list_to_sort, cmp=/proc/cmp_text_asc)` __HELPERS/_lists.dm:733 | `public static object sort_list(DmList list_to_sort = null, string cmp = null)` | 0 | |
| `/proc` `assert_sorted` | `assert_sorted(list/list, name, cmp = GLOBAL_PROC_REF(cmp_numeric_asc))` _lists.dm:1213 | `public static void assert_sorted(DmList list = null, object name = null, string cmp = null)` | 0 | |
| `/proc` `update_midnight_rollover` | `update_midnight_rollover()` __HELPERS/time.dm:19 | `public static float update_midnight_rollover()` (decided, W3.0 contracts: narrowed by 7.4, its only exit returns `GLOB.midnight_rollovers`, a number never null, so that `REALTIMEOFDAY` is a `float` member, `__DEFINES/maths.cs`) | 0 | |
| `/datum/log_category` `get_output_file` | `get_output_file(list/entry, extension = "log.json")` modules/logging/log_category.dm:57 | `public virtual object get_output_file(object entry = null, string extension = null)` (decided, wave 3 logging: `entry` retyped by a "-" cell of 3.5, `write_entry` passes the `/datum/log_entry`, log_category.dm:68, :70) | 0 | |
| `/proc` `cmp_subsystem_init_stage` | `cmp_subsystem_init_stage(datum/controller/subsystem/a, datum/controller/subsystem/b)` __HELPERS/cmp.dm:76 | `public static object cmp_subsystem_init_stage(object a = null, object b = null)` (decided, wave 3 mc: both callers sort lists of subsystem typepaths, master.dm:111, failsafe.dm:155; `initial()` reads a typepath or an instance) | 0 | |
| `/datum/controller/configuration` `LoadEntries` | `LoadEntries(filename, list/stack = list())` controllers/configuration/configuration.dm:176 | `public virtual DmTask<object> LoadEntries(object filename = null, DmList stack = null)` (decided, wave 3 boot: `slots.tsv` says `void`, but `LoadEntries` sets `.` with `++.` and `Load` reads the result, configuration.dm:97) | 0 | |
| `/proc` `log_combat` | `log_combat(atom/user, atom/target, what_done, atom/object=null, addition=null)` __HELPERS/logging/attack.dm:15 | `public static void log_combat(atom user = null, atom target = null, object what_done = null, object @object = null, object addition = null)` (decided, M2 integration: `object` retyped by a "-" cell of 3.5; the disarm passes text there, `living_defense.dm:855`, `:868`, `:886`) | 0 | |
| `/atom` `flick_overlay_view` | `flick_overlay_view(mutable_appearance/display, duration)` __HELPERS/game.dm:124 | `public virtual object flick_overlay_view(object display = null, object duration = null)` (decided, M4v slice 3: `slots.tsv` leaves `display` undecided by 3.4; tg passes an icon_state text, `buttons.dm:303`, or an `/image`, `items.dm:1536`, told apart by `istext` at `:129`) | 0 | |
| `/mob` `playsound_local` | `playsound_local(turf/turf_source, soundin, vol as num, ..., datum/preference/numeric/volume/volume_preference = null, min_volume = 5)` game/sound/sound.dm:101 | `public virtual object playsound_local(atom turf_source = null, object soundin = null, object vol = null, object vary = null, object frequency = null, float? falloff_exponent = null, float? channel = null, float? pressure_affected = null, sound sound_to_use = null, object max_distance = null, float? falloff_distance = null, float? distance_multiplier = null, float? use_reverb = null, object volume_preference = null, float? min_volume = null)` (decided, M4b audio slice 1: `turf_source` retyped by a "-" cell of 3.5, callers pass a mob or an obj, `sound.dm:190`, `janitor_key.dm:55`, `dance_machine.dm:72`, and the proc tests it with `isturf` at `:120`) | 0 | |
| `/proc` `playsound` | `playsound(atom/source, soundin, ..., datum/preference/numeric/volume/volume_preference = null, min_volume = 3)` game/sound/sound.dm:19 | `public static object playsound(atom source = null, object soundin = null, object vol = null, object vary = null, object extrarange = null, float? falloff_exponent = null, object frequency = null, float? channel = null, float? pressure_affected = null, float? ignore_walls = null, float? falloff_distance = null, float? use_reverb = null, object volume_preference = null, float? min_volume = null)` (decided, M4b audio slice 1: `volume_preference` retyped by a "-" cell of 3.5, callers pass a preference typepath, `radio.dm:739`, which `playsound_local` tests with `ispath`) | 0 | |
| `/atom` `run_atom_armor` | `run_atom_armor(damage_amount, damage_type, damage_flag = 0, attack_dir, armour_penetration = 0)` game/atom/atom_defense.dm:92 | `public virtual DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)` (decided, M5 slice 1: `damage_flag` retyped by a "-" cell of 3.5; take_damage passes tg's text flags, MELEE, FIRE, BOMB, `atom_defense.dm:28`, and the root compares it with MELEE, `:95`) | 0 | |
| `/atom` `play_attack_sound` | `play_attack_sound(damage_amount, damage_type = BRUTE, damage_flag = 0)` game/atom/atom_defense.dm:107 | `public virtual object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)` (decided, M5 slice 1: `damage_flag` retyped by a "-" cell of 3.5, as run_atom_armor's; take_damage passes its text flag, `atom_defense.dm:25`) | 0 | |
| `/atom` `attack_generic` | `attack_generic(mob/user, damage_amount = 0, damage_type = BRUTE, damage_flag = 0, sound_effect = 1, armor_penetration = 0)` game/atom/atom_defense.dm:121 | `public virtual DmTask<object> attack_generic(mob user = null, float? damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, float? armor_penetration = null)` (decided, M5 slice 1: `damage_flag` retyped by a "-" cell of 3.5; `/obj/attack_alien` and `/obj/attack_animal` pass MELEE, `obj_defense.dm:69`, `:81`) | 0 | |
| `/atom` `set_armor` | `set_armor(datum/armor/armor)` datums/armor/_atom_armor.dm:12 | `public virtual void set_armor(object armor = null)` (decided, M5 slice 1: the undecided `armor`, slots.tsv's note, is `object`: tg passes a typepath as well as a datum, `armor_verification.dm:7`, and the proc tests it with `ispath`, `_atom_armor.dm:17`) | 0 | |
| `/proc` `sound_to_playing_players` | `sound_to_playing_players(soundin, volume = 100, ..., sound/S, datum/preference/numeric/volume/volume_preference = null)` game/sound/sound.dm:184 | `public static void sound_to_playing_players(object soundin = null, float? volume = null, float? vary = null, float? frequency = null, float? channel = null, float? pressure_affected = null, sound S = null, object volume_preference = null)` (decided, M4b audio slice 1: `volume_preference` as `playsound`'s, `security_level.dm:23`) | 0 | |
| `/datum/controller/subsystem/sounds` `free_datum_channels`, `stop_tracking_datum` | `free_datum_channels(datum/channel_owner)` controllers/subsystem/sounds.dm:124, `stop_tracking_datum(datum/channel_owner)` :268 | `public virtual void free_datum_channels(object channel_owner = null)`, `public virtual void stop_tracking_datum(object channel_owner = null)` (decided, M4b audio slice 1: retyped by a "-" cell of 3.5, `free_datumless_channels` passes the text `DATUMLESS`, `:135`, and `free_sound_channel` the channel's owner read from an assoc list, `:120`; `UnregisterSignal` casts it to `datum` at the call, where DM fails on text too) | 0 | |
| `/obj/machinery/suit_storage_unit` `create_silhouette_of` | `create_silhouette_of(atom/item)` game/machinery/suit_storage_unit.dm:444 | `public virtual object create_silhouette_of(object item = null)` (decided, M4c slice 4: retyped by a "-" cell of 3.5, its only callers pass typepaths, `:341-345`, and it reads `initial(item.icon)` of the type) | 0 | |
| `/datum/controller/subsystem/materials` `get_id_from_args` | `get_id_from_args(datum/material/mat_type, ...)` controllers/subsystem/materials.dm:158 | `public virtual object get_id_from_args(object mat_type = null, params object[] __extra)` (decided, M5 slice 2: the undecided `mat_type`, slots.tsv's note, is `object`: tg's callers pass the material's type, which `initial(mat_type.id)` reads) |
| `/atom` `has_material_type` | `has_material_type(datum/material/required_material, mat_amount = 0)` game/atom/atom_materials.dm:535 | `public virtual DmTask<object> has_material_type(object required_material = null, float? mat_amount = null)` (decided, M5 slice 2: retyped by a "-" cell of 3.5: tg passes a material typepath for the `istype`) |
| `/atom` `set_greyscale` | `set_greyscale(list/colors, new_config)` game/atom/atom_greyscale.dm:10 | `public virtual DmTask set_greyscale(object colors = null, object new_config = null, params object[] __extra)` (decided, M5 slice 2: retyped by a "-" cell of 3.5: tg passes a list or the joined colour text, atom_materials.dm:416) |
| `/obj/item/stack/tile` `tile_dir_list` | `tile_dir_list(list/values, atom/type_cast_path)` game/objects/items/stacks/tiles/tile_reskinning.dm:28 | `public virtual object tile_dir_list(DmList values = null, object type_cast_path = null)` (decided, M5 slice 2: the undecided `type_cast_path` is `object`: its caller passes the tile's `turf_type`, a typepath that `initial()` reads) |
| `/proc` `dyn_explosion` | `dyn_explosion(turf/epicenter, power, flame_range = 0, flash_range = null, adminlog = TRUE, ignorecap = TRUE, silent = FALSE, smoke = TRUE, atom/explosion_cause = null)` controllers/subsystem/explosions.dm:185 | `public static DmTask dyn_explosion(atom epicenter = null, object power = null, float? flame_range = null, object flash_range = null, float? adminlog = null, float? ignorecap = null, float? silent = null, float? smoke = null, atom explosion_cause = null)` (decided, M5x slice 3: `epicenter` widened to `/atom`; the ruptured tank passes itself, `tanks.dm:402`, and the value only reaches `explosion()`'s `atom/origin`) | 0 | |
| `/datum/element/atmos_sensitive` `react_to_move` | `react_to_move(atom/source, atom/movable/oldloc, direction, forced)` datums/elements/atmos_sensitive.dm:31 | `public virtual void react_to_move(atom source = null, atom oldloc = null, object direction = null, object forced = null)` (decided, M5x slice 6: `oldloc` widened to `/atom`; `COMSIG_MOVABLE_MOVED` passes the old loc, a turf, `atoms_movable.dm`'s Moved) | 0 | |
| `/mob/living` `damage_clothes` | `damage_clothes(damage_amount, damage_type = BRUTE, damage_flag = 0, def_zone)` modules/mob/living/living_defense.dm:742 | `public virtual DmTask damage_clothes(object damage_amount = null, string damage_type = null, object damage_flag = null, object def_zone = null)` (decided, M5x slice 3: `damage_flag` retyped by a "-" cell of 3.5, as run_atom_armor's; the human's blast passes BOMB, `human_defense.dm:349`) | 0 | |
| `/datum/wound` `apply_wound` | `apply_wound(obj/item/bodypart/limb, silent = FALSE, datum/wound/old_wound = null, smited = FALSE, attack_direction = null, wound_source = "Unknown", replacing = FALSE)` datums/wounds/_wounds.dm:193 | `public virtual DmTask<object> apply_wound(obj_item_bodypart limb = null, float? silent = null, datum_wound old_wound = null, float? smited = null, object attack_direction = null, object wound_source = null, float? replacing = null, params object[] __extra)` (decided, M6 slice 2: `wound_source` widened from text to any value; `check_wounding` passes the damage source, an atom, `bodyparts/wounds.dm:131`, and the proc keeps an atom's name, `_wounds.dm:197-202`) | 0 | |
| `/mob/living/carbon` `gain_trauma`, `/obj/item/organ/brain` `gain_trauma`, `can_gain_trauma` | `gain_trauma(datum/brain_trauma/trauma, resilience, ...)` modules/mob/living/carbon/status_procs.dm:28, brain/brain_item.dm:658; `can_gain_trauma(datum/brain_trauma/trauma, resilience, natural_gain = FALSE)` brain_item.dm:619 | `public virtual DmTask<object> gain_trauma(object trauma = null, object resilience = null, params object[] __extra)` (both), `public virtual object can_gain_trauma(object trauma = null, object resilience = null, float? natural_gain = null)` (decided, the blunt head trauma 2026-10-20: `trauma` retyped by a "-" cell of 3.5; tg passes a trauma typepath, `item_attack.dm:446`, as well as an instance, which `can_gain_trauma` tells apart with `ispath`, `brain_item.dm:623`, as `brain_gain_trauma`'s `object` already does) | 0 | |
| `/proc` `supermatter_anomaly_gen` | `supermatter_anomaly_gen(turf/anomalycenter, type = FLUX_ANOMALY, anomalyrange = 5, has_changed_lifespan = TRUE)` modules/power/supermatter/supermatter_extra_effects.dm:162 | `public static void supermatter_anomaly_gen(object anomalycenter = null, string type = null, float? anomalyrange = null, float? has_changed_lifespan = null)` (decided, the M7 delamination slice: `anomalycenter` retyped by a "-" cell of 3.5; the delamination's `effect_anomaly` passes the event spawn landmarks, `delamination_effects.dm:68`, `:76`, and the value only reaches `RANGE_TURFS`'s center) | 0 | |
| `/turf/open` `replace_floor` | `replace_floor(turf/open/new_floor_path, flags)` game/turfs/open/_open.dm:288 | `public virtual DmTask replace_floor(Type new_floor_path = null, object flags = null)` (decided, the M7 delamination slice: `new_floor_path` is a `Type` by V3 applied to the param; every caller passes a floor's typepath, `anomalies_dimensional_themes.dm:136`, and the body reads it with `initial()` and hands it to `ChangeTurf` and `place_on_top`) | 0 | |
| `/obj/machinery/atmospherics/components/unary/hypertorus/core` `unregister_signals` | `unregister_signals(only_signals = FALSE)` modules/atmospherics/machinery/components/fusion/hfr_procs.dm:118 | `public virtual void unregister_signals(object only_signals = null)` (decided, engineering E12b: `only_signals` is `object` by a "-" cell of 3.5; it is also the `COMSIG_QDELETING` handler of the linked parts, `hfr_procs.dm:95-108`, so the deleted part arrives in it and is truthy: only the signals are dropped, as in tg) | 0 | |
| `/datum/ai_controller` `change_ai_movement_type`, `/datum/bt_node/ai_behavior/agile_melee_movement` `set_movement_mode` | `new_movement`, `movement_type`: declared `datum/ai_movement`, called with a typepath that indexes `SSai_movement.movement_types` | `object` (the slot's `datum_ai_movement` could not hold the path; gaps C1) |
| `/datum/reagents` `trans_to` | `trans_to(atom/target, amount = 1, multiplier = 1, target_id, preserve_data = TRUE, no_react = FALSE, mob/transferred_by, remove_blacklisted = FALSE, methods = NONE, show_message = TRUE, ignore_stomach = FALSE, copy_only = FALSE)` modules/reagents/chemistry/holder/holder.dm:422 | `remove_blacklisted` is `object` (decided, engineering E15: a "-" cell of 3.5; the plumbing filter's override passes a list of reagent types in it, `_plumb_reagents.dm:245`) | 0 | |
| `/obj/item/paper` `copy` | `copy(paper_type = /obj/item/paper, atom/location = loc, colored = TRUE, greyscale_override = null)` modules/paperwork/paper.dm:155 | `paper_type` is `object` (decided, gaps S3: the photocopier passes the empty paper instance, `photocopier.dm:547`, tg's own `istype` branch) | 0 | |
| `/obj/item/paperwork` `copy_stamp_info` | `copy_stamp_info(obj/item/paperwork/paperwork_type)` modules/paperwork/paperwork.dm:119 | `paperwork_type` is `object` (decided, gaps S3: the ancient paperwork passes a typepath, the photocopier an instance; `initial()` reads either) | 0 | |
| `/obj/item/pen` `on_inserted_into_dart` | `on_inserted_into_dart(datum/source, obj/projectile/dart, mob/user, embedded = FALSE)` modules/paperwork/pen.dm:88 | `dart` is `object` (decided, gaps S3: the dart insert component passes the casing, which the sleepy, survival and energy dagger pens read as one) | 0 | |
| `/mob/living/basic/bot` `update_bot_mode` | `update_bot_mode(new_mode, update_hud = TRUE)` modules/mob/living/basic/bots/_bots.dm:832 | `public virtual void update_bot_mode(object new_mode = null, object update_hud = null)` (the cleaner's callbacks append their source mob here: truthy) | 0 | |
| `/datum/lootpanel` `add_new_searchable` | `add_new_searchable(atom/movable/thing, from_signal)` modules/lootpanel/contents.dm:30 | `thing` is `atom` (decided, gaps G1: tg passes the source turf to it, a turf in an `atom/movable` parameter) | 0 | |
| `/mob` `slip` | `slip(knockdown_amount, obj/slipped_on, lube_flags, paralyze, daze, force_drop)` modules/mob/mob_movement.dm:380 | `slipped_on` is `atom` (decided, gaps G4: a wet floor's slippery component passes the turf; the slip memory and its chat line name it) | 0 | |
| `/atom` `handle_slip` | `handle_slip(mob/living/carbon/slipped_carbon, knockdown_amount, obj/slipping_object, lube, paralyze, daze, force_drop)` game/atom/atom_act.dm:162 (and `/turf/open`'s `obj/slippable`, _open.dm:540) | `slipping_object` is `atom` (decided, gaps G4: as `slip`'s `slipped_on`) | 0 | |
| `/obj/machinery/power/manufacturing` `send_resource` | `send_resource(atom/movable/sending, atom/what_or_dir)` modules/manufactorio/_manufacturing.dm:91 | `public virtual DmTask<object> send_resource(atom_movable sending = null, object what_or_dir = null)` (decided, engineering E16: `what_or_dir` retyped by a "-" cell of 3.5: every caller but `isobj`'s own branch passes a direction number, `crusher.dm:44`, `sorter.dm:122`, `unloader.dm:68`, and the body tests it with `isobj` and `isturf`) | 0 | |
| `/obj/machinery/big_manipulator` `try_press_on`, `try_use_thing` | `try_press_on(mob/living/carbon/human/user)` game/machinery/big_manipulator/big_manipulator.dm:416, `try_use_thing(datum/manipulator_task/cargo/interact/destination_task)` big_manipulator_interactions.dm:118 | `public virtual DmTask try_press_on(mob user = null)`, `public virtual DmTask try_use_thing(datum_manipulator_task_cargo destination_task = null)` (decided, engineering E16, by "-" cells of 3.5: the right click and the window pass any mob, and a null user would skip `toggle_power_state`'s checks; the use task, a `/cargo/dropoff_base` subtype, calls `try_use_thing` too, which reads its same-named settings by name) | 0 | |
| `/obj/machinery/power/weather_tower` `summon_weather` | `summon_weather(datum/weather/weather_type, mob/user)` game/machinery/mining_weather_monitor.dm:251 | `public virtual DmTask<object> summon_weather(Type weather_type = null, mob user = null)` (decided, engineering E16: `weather_type` is a `Type` by V3 applied to the param; its only caller passes `text2path(...)` and the body reads `weather_type::weather_flags`) | 0 | |
| `/mob/living/carbon` `create_dna` | `create_dna(datum/species/species)` datums/dna/dna.dm:475 | `public virtual void create_dna(object species = null)` (decided, gaps C5: a type or TRUE: `/mob/living/carbon/human/species` passes `race`, a typepath, and a monkey cube's `new spawned_mob(loc, TRUE)` lands in this slot (monkeycube.dm:42)) | 0 | |
| `/proc` `should_visual_organ_apply_to` | `should_visual_organ_apply_to(obj/item/organ/organpath, mob/living/carbon/target)` organs/external/_visual_organs.dm:52 | `public static object should_visual_organ_apply_to(object organpath = null, mob_living_carbon target = null)` (decided, gaps C5: an instance from `regenerate_organs`, a typepath from the mutant overlay's `on_mob_insert`) | 0 | |
| `/datum/targeting_strategy/pickup_item/food_or_drink` `_is_food`, `_is_drink` | `_is_food(obj/item/thing)` targeting_strategies/food_or_drink.dm:11 | `public virtual object _is_food(atom thing = null)` (decided, gaps C5: the target sources hand it any atom in view; DM reads the var on any of them) | 0 | |
| `/datum/pet_command/beehive` `check_beehive_conditions` | `check_beehive_conditions(obj/structure/hive)` modules/mob/living/basic/farm_animals/bee/bee_ai_behavior.dm:76 | `public virtual object check_beehive_conditions(object hive = null, params object[] __extra)` (decided, gaps P2: `hive` is `object` by a "-" cell of 3.5; the caller, `try_activate_command` at :73, and both overrides, `enter` and `exit` at :87 and :97, pass and take `(mob/living/living_pawn, obj/structure/hive)`, so the pawn arrives first) | 2 | |
| `/mob/living` `perform_surgery`, `get_available_operations` | `perform_surgery(atom/movable/operating_on, potential_tool = IMPLEMENT_HAND, intentionally_fail = FALSE, operating_zone = zone_selected)` surgery/operations/_operation.dm:11, `:97` | `public virtual DmTask<object> perform_surgery(atom_movable operating_on = null, object potential_tool = null, float? intentionally_fail = null, object operating_zone = null)`, `get_available_operations(atom_movable operating_on = null, object potential_tool = null, object operating_zone = null)` (decided, gaps P1: `potential_tool` retyped by a "-" cell of 3.5; it is an item or the text `IMPLEMENT_HAND`, `item_attack.dm:189`, `living_defense.dm:511`) | 0 | |
| `/mob/living` `surgery_check` | `surgery_check(obj/item/tool, atom/movable/operating_on)` surgery/operations/_operation.dm:122 | `public virtual object surgery_check(object tool = null, atom_movable operating_on = null)` (decided, gaps P1: `tool` retyped by a "-" cell of 3.5; `try_perform` hands it its tool, an item or `IMPLEMENT_HAND`, `:951`) | 0 | |
| `/datum/surgery_operation` `get_tool_quality` | `get_tool_quality(tool = IMPLEMENT_HAND)` surgery/operations/_operation.dm:511 | `public virtual object get_tool_quality(object tool = null)` (decided, gaps P1: the generator typed it `string` from its default; tg passes the item in hand as often, `:478`, `:741`) | 0 | |
| `/proc` `cmp_name_asc` | `cmp_name_asc(atom/a, atom/b)` __HELPERS/cmp.dm:33 | `public static object cmp_name_asc(object a = null, object b = null)` (decided, gaps P1: `a`, `b` retyped by a "-" cell of 3.5; `perform_surgery` sorts the radial's `/datum/radial_menu_choice` slices with it, `_operation.dm:62`, which DM reads by `name` like an atom: `Dm.get_var`) | 0 | |
| `/datum/status_effect/bioware/cortex/imprinted` `on_gain_trauma` | `on_gain_trauma(datum/source, datum/brain_trauma/trauma, resilience)` status_effects/buffs/bioware/cortex.dm:21 | `public virtual object on_gain_trauma(datum source = null, object trauma = null, object resilience = null)` (decided, gaps P1: `trauma` retyped by a "-" cell of 3.5; the brain's signal sends the trauma it was given, an instance or a typepath, `brain_item.dm:690`) | 0 | |
| `/proc` `create_uplink_sales` | `create_uplink_sales(num, datum/uplink_category/category, limited_stock, list/sale_items)` modules/uplink/uplink_items.dm:3 | `public static DmList create_uplink_sales(object num = null, Type category = null, object limited_stock = null, DmList sale_items = null)` (decided, syndicate A2: `category` is a `Type` by V3; its callers pass a category's typepath, `datum_traitor.dm:84`, and the body stores it in the entry's `category`, a typepath var) | 0 | |
| `/proc` `cmp_uplink_category_desc` | `cmp_uplink_category_desc(datum/uplink_category/compare1, datum/uplink_category/compare2)` __HELPERS/cmp.dm:171 | `public static object cmp_uplink_category_desc(object compare1 = null, object compare2 = null)` (decided, syndicate A2: retyped by a "-" cell of 3.5; the uplink asset sorts the categories' typepaths with it, `asset_cache/assets/uplink.dm:12`, and the body reads `initial(weight)`) | 0 | |
| `/datum/dynamic_ruleset` `get_candidate_mind` | `get_candidate_mind(mob/dead/candidate)` controllers/subsystem/dynamic/_dynamic_ruleset.dm:246 | `public virtual object get_candidate_mind(mob candidate = null)` (decided, syndicate A3: `candidate` widened to `/mob`; the from-living rulesets pass living mobs, `prepare_execution`'s loop var is a `/mob`, `:236`) | 0 | |
| `/datum/dynamic_ruleset/roundstart` `ruleset_forced_job_check` | `ruleset_forced_job_check(mob/candidate, client/candidate_client, datum/job/job_typepath)` dynamic_ruleset_roundstart.dm:18 | `public virtual DmTask<object> ruleset_forced_job_check(mob candidate = null, client candidate_client = null, Type job_typepath = null)` (decided, syndicate A3: `job_typepath` is a `Type` by V3; the malfunctioning AI passes `/datum/job/ai` and the body reads `job_typepath::title`) | 0 | |
| `/datum/dynamic_panel` `ruleset_to_data` | `ruleset_to_data(datum/dynamic_ruleset/ruleset)` dynamic_admin.dm:91 | `public virtual object ruleset_to_data(object ruleset = null)` (decided, syndicate A3: "Pass a ruleset typepath or a ruleset instance", the body tests `isdatum`) | 0 | |
| `/datum/antagonist` `render_preview_outfit`, `finish_preview_icon`; `/proc` `get_flat_uni_icon` | `render_preview_outfit(datum/outfit/outfit, mob/living/carbon/human/dummy)` antag_datum.dm:480, `finish_preview_icon(datum/universal_icon/antag_icon)` :494, `get_flat_uni_icon(image/appearance, ...)` universal_icon.dm:402 | `outfit`, `antag_icon` and `appearance` are `object` (decided, syndicate A3: `get_preview_icon` passes the `preview_outfit` typepath; the port's universal icon is the `/icon` recipe, universal_icon.cs; the flattened appearance is the dummy, an atom) | 0 | |
| `/datum/chemical_reaction` `chemical_mob_spawn` | `chemical_mob_spawn(datum/reagents/holder, amount_to_spawn, reaction_name, mob_class = HOSTILE_SPAWN, mob_faction = FACTION_CHEMICAL_SUMMON, random = TRUE)` modules/reagents/chemistry/recipes.dm:185 | `public virtual DmTask chemical_mob_spawn(datum_reagents holder = null, object amount_to_spawn = null, object reaction_name = null, object mob_class = null, string mob_faction = null, float? random = null)` (decided, gaps P5: `mob_class` retyped by a "-" cell of 3.5; a spawn class number when `random`, `create_random_mob`'s, and the mob type `new mob_class(...)` makes when not, recipes.dm:204) | 0 | |
| `/obj/machinery/chem_heater` `move_buffer` | `move_buffer(datum/reagent/buffer_type, volume)` modules/reagents/chemistry/machinery/chem_heater.dm:359 | `public virtual DmTask<object> move_buffer(Type buffer_type = null, object volume = null)` (decided, gaps P5: the undecided `buffer_type` is a `Type`; both callers pass a buffer's typepath, :330 and :340, and the body reads it with `initial()` and `get_reagent_amount`) | 0 | |
| `/obj/machinery/chem_master` `transfer_reagent` | `transfer_reagent(datum/reagents/source, datum/reagents/target, datum/reagent/path, amount, do_transfer)` modules/reagents/chemistry/machinery/chem_master.dm:372 | `public virtual DmTask<object> transfer_reagent(datum_reagents source = null, datum_reagents target = null, object path = null, object amount = null, object do_transfer = null)` (decided, gaps P5: `path` retyped by a "-" cell of 3.5; the window sends the reagent's path as text, `ui_act` at :461, which the body turns into a type with `text2path`, :379) | 0 | |
| `/proc` `cmp_reagents_asc` | `cmp_reagents_asc(datum/reagent/a, datum/reagent/b)` __HELPERS/cmp.dm:150 | `public static object cmp_reagents_asc(object a = null, object b = null)` (decided, gaps P5: the undecided `a`, `b` are `object`; the dispenser sorts its lists of reagent typepaths with it, chem_dispenser.dm:103-113, and `initial(a.name)` reads a type or an instance alike, 10.3) | 0 | |
| `/obj/structure/closet` `close`, `insert`, `insertion_allowed`, `take_contents` | `close(mob/living/user)` crates_lockers/closets.dm:612, `insert(atom/movable/inserted, mapload = FALSE)` :562, `insertion_allowed(atom/movable/AM)` :575, `take_contents(mapload = FALSE)` :521 | `close(atom_movable user = null)`, `insert(object inserted = null, object mapload = null)`, `insertion_allowed(atom AM = null)`, `take_contents(object mapload = null)` (decided, syndicate G4c: `user` widened to `/atom/movable`, `mapload` to any value and `AM` to `/atom` by "-" cells of 3.5; the supply pod closes over its holder, itself, and passes it as the second argument, and asks `insertion_allowed` of the turf it lands on, supplypod.dm:428-474) | 0 | |
| `/obj/item/antag_spawner` `spawn_antag` | `spawn_antag(client/C, turf/T, kind = "", datum/mind/user)` antagonists/_common/antag_spawner.dm:8 | `public virtual DmTask spawn_antag(client C = null, turf T = null, object kind = null, datum_mind user = null)` (decided, syndicate G4c: `kind` widened from text to any value by a "-" cell of 3.5; the loadout beacons pass their user there, antag_spawner.dm:369) | 0 | |
| `/atom` `AICtrlClick`, `ai_click_alt`, `AIShiftClick`, `AICtrlShiftClick` | `AICtrlClick(mob/living/silicon/ai/user)` _onclick/ai.dm:160, `ai_click_alt` :163, `AIShiftClick` :167, `AICtrlShiftClick` :170 | `AICtrlClick(mob_living_silicon user = null)` and the others alike (decided, silicons SI2: `user` widened from the AI to `/mob/living/silicon`; the cyborgs' Borg* clicks pass the cyborg, _onclick/cyborg.dm:110-182, and no override reads an AI-only member) | 0 | |
| `/obj/item/reagent_containers/borghypo` `add_new_reagent` | `add_new_reagent(datum/reagent/reagent)` robot/items/hypo.dm:167 | `public virtual DmTask add_new_reagent(object reagent = null)` (decided, silicons SI2: `reagent` widened to any value by a "-" cell of 3.5; every caller passes a reagent typepath, hypo.dm:149, :272) | 0 | |
| `/obj/item` `do_pickup_animation` | `do_pickup_animation(atom/target, turf/source)` game/objects/items.dm | `public virtual void do_pickup_animation(atom target = null, atom source = null)` (decided, silicons SI2: `source` widened to `/atom`; the cyborg's vacuum suite passes the offering cyborg, robot/items/janitor.dm:86, and the proc only reads it as an image's location and a direction) | 0 | |
| `/datum/saymode` `handle_message` | `handle_message(mob/living/user, message, list/spans, datum/language/language, list/message_mods)` datums/saymode.dm:23 | `public virtual DmTask<object> handle_message(mob user = null, object message = null, DmList spans = null, object language = null, DmList message_mods = null)` (decided, gaps K1: `user` retyped by a "-" cell of 3.5; the changeling hivemind's member, a `/mob/eye`, speaks through the changeling say mode, `absorb.dm:212`; the changeling say mode's `is_muted(mob/living/user)` takes the same `mob`, since `get_lings` passes the hivemind's eyes) | 0 | |
| `/obj/item/organ/stomach` `handle_disgust` | `handle_disgust(mob/living/carbon/human/disgusted, seconds_per_tick)` surgery/organs/internal/stomach/_stomach.dm:331 | `public virtual DmTask handle_disgust(mob_living_carbon disgusted = null, object seconds_per_tick = null)` (decided, gaps X5: `disgusted` widened to `/mob/living/carbon`; `on_life` passes any carbon body, a monkey's or an alien's, and every member read is a carbon's) | 0 | |
| `/atom` `attack_alien` | `attack_alien(mob/living/carbon/alien/user, list/modifiers)` _onclick/other_mobs.dm:217 | `public virtual DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)` (decided, gaps K1b: `user` widened to `/mob/living/carbon`; the changeling's arm blade passes its wielder, a human, on a computer, `antagonists/changeling/powers/mutations.dm:238`, into `/obj/attack_alien`; the 23 definitions take the carbon, the aliens' own callers pass themselves) | 0 | |
| `/datum/experiment/scanning` `serialize_progress_stage` | `serialize_progress_stage(atom/target, list/seen_instances)` modules/experisci/experiment/types/scanning.dm:72 | `public virtual DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)` (decided, gaps RD3: the undecided `target` is `Type`; `check_progress` passes each typepath of `required_atoms`, read through a typed `/atom` local, and every definition reads `initial(target.name)` and the lists by it) | 0 | |
| `/obj/machinery/computer/communications` `get_department_employee_count` | `get_department_employee_count(datum/job_department/target_department_type)` game/machinery/computer/communications.dm:761 | `public virtual object get_department_employee_count(object target_department_type = null)` (decided, gaps CM1: retyped by a "-" cell of 3.5; every caller passes a department's typepath, `dept_shuttle.department_type` at :277 and :632, or a department's name, `other_shuttle.department_name` at :640, and the body only uses it as a key of `SSjob.joinable_departments_by_type`) | 0 | |
| `/proc` `explosion`; `/datum/controller/subsystem/explosions` `explode`, `propagate_blastwave` | `explosion(atom/origin, ..., atom/explosion_cause = null, ...)` controllers/subsystem/explosions.dm:213, `explode` :236, `propagate_blastwave` :300 | `object explosion_cause = null` in all three (decided, gaps OD2: retyped by a "-" cell of 3.5; tg passes a text cause, `"[firer] using \\the [fired_from]"` at `items/v8_engine.dm:188` and `"growing vat too small"` at `vatgrowing/vatgrower.dm:206`, which the log prints and `istype(explosion_cause)` at `:357` tells from an atom: `explosion_cause is atom`) | 0 | |
| `/obj/item/house_edge` `reset_charges` | `reset_charges(on_slash = FALSE)` items/v8_engine.dm:128 | `public virtual DmTask reset_charges(object on_slash = null)` (decided, gaps OD2: retyped by a "-" cell of 3.5; as the handler of the drop, pre-throw and use-in-hand signals, `:81`, it is handed the signal's source, the sword, which DM reads as a true `on_slash`) | 0 | |
| `/proc` `los_check` | `los_check(atom/movable/user, mob/target, pass_args = PASSTABLE\|PASSGLASS\|PASSGRILLE, datum/callback/mid_check)` __HELPERS/atoms.dm:375 | `public static DmTask<object> los_check(atom_movable user = null, atom target = null, float? pass_args = null, datum_callback mid_check = null)` (decided, gaps MO2: `target` widened to `/atom`; the MOD teleporter module passes the turf it jumps to, `modules_science.dm:116`, and the proc reads it only as the line's end point) | 0 | |
| `/obj/machinery/quantum_server` `generate_avatar`, `start_new_connection` | `generate_avatar(turf/destination, datum/outfit/netsuit)` bitrunning/server/obj_generation.dm:41, `start_new_connection(mob/living/carbon/human/neo, datum/outfit/netsuit)` server/util.dm:144 | `public virtual DmTask<object> generate_avatar(turf destination = null, Type netsuit = null)`, `public virtual DmTask<object> start_new_connection(mob_living_carbon_human neo = null, Type netsuit = null)` (decided, gaps BR1: `netsuit` retyped `Type` by P1's test: tg's callers pass the netpod's outfit path, `netpod/utils.dm:95`, and `generate_avatar` makes the outfit with `new outfit_path()`, `obj_generation.dm:45`) | 0 | |
| `/obj/machinery/quantum_server` `setup_glitch`, `spawn_glitch` | `setup_glitch(datum/antagonist/bitrunning_glitch/forced_role)` server/threats.dm:65, `spawn_glitch(datum/antagonist/bitrunning_glitch/chosen_role, mob/living/mutation_target, mob/dead/observer/ghost)` :93 | `public virtual DmTask<object> setup_glitch(Type forced_role = null)`, `public virtual DmTask spawn_glitch(Type chosen_role = null, mob_living mutation_target = null, mob_dead_observer ghost = null)` (decided, gaps BR1: `forced_role` and `chosen_role` retyped `Type` by P1's test: the role is a typepath, given to `add_antag_datum` and read with `initial(chosen_role.name)`, `:77`, `:102`) | 0 | |

Parameters of these slots that `slots.tsv` lists as undecided (they keep the declared type until the root's translator decides them, 3.3): `/atom` `Entered` `arrived` and `/atom` `Exited` `gone` (a type test against a computed type, `datums/components/udder.dm:126` and `:135`; `gone` is also read by `initial()`), `/atom` `Bumped` `bumped_atom` (`structures/transit_tubes/station.dm:253`), `/atom` `CanAllowThrough` `mover` (`effects/forcefields.dm:34`) and `/atom/movable` `Move` `newloc` (`antagonists/blob/overmind.dm:391`), all a type test nested in a condition; `/atom/movable` `Bump` `bumped_atom` (read on the other-kind path only in a `CRASH`/log message, `events/immovable_rod/immovable_rod.dm:198`). (`/datum/element` `Detach` `source` is decided in its row.) Decided in wave 4: `/datum/movement_detector` `track` `target` keeps `atom/movable` (`datums/movement_detector.dm`: the `while(ismovable(target))` test fails only after `target.loc` made it a turf or null, and nothing reads it after that, so the `as atom_movable` downcast ends the loop as DM's test does); `/turf` `assemble_baseturfs` `fake_baseturf_type` is `object` (a list or a typepath, told apart with `length()`, `turfs/turf.dm:469`); `/atom` `add_overlay` `add_overlays` and `/atom` `cut_overlay` `remove_overlays` are `object` (callers pass single overlays that `build_appearance_list` wraps), and the var `managed_overlays` of `/atom` is `object` (tg stores a single overlay in it, `atoms_movable.dm:181`); `/atom/movable` `could_speak_language` `language_path` is `Type` (every caller passes a language typepath, `_language_holder.dm:241`).

Other per-proc decisions recorded with these slots: the var `cmp` of `/datum/sort_instance` (`sorts/sort_instance.dm:18`) is `string` (a proc reference, 3.2); and these typed locals of slot definitions are `object` by 3.4: `var/list/components` in `/datum/proc/GetComponents` (`_component.dm:297`: one component or a list of them, told apart with `islist`), `var/datum/component/component_type` in `/datum/proc/_AddComponent` (`_component.dm:315`: a typepath or a component instance, told apart with `ispath` and `istype`), and `var/datum/element/eletype` in `/datum/controller/subsystem/processing/dcs/proc/GetElement` (`controllers/subsystem/dcs.dm:36`: tested with `ispath`).

## Appendix B. Name collisions at the snapshot

Found by a regex scan of `../tgstation/code` (approximate; the type-tree converter will produce the exact lists).

**B.1 Class-name collisions (2):** `/datum/action/innate/mimic/lock` (`datum_action_innate_mimic_lock`) and `/datum/action/innate/mimic_lock` (`datum_action_innate_mimic_lock__2`); `/obj/projectile/energy/tesla/cannon` (`obj_projectile_energy_tesla_cannon`) and `/obj/projectile/energy/tesla_cannon` (`obj_projectile_energy_tesla_cannon__2`).

**B.2 Var/proc name clashes along one ancestor line (71 names; the proc becomes `<name>__proc`; M9 added `say_mod`, which the regex scan missed: `/obj/item/organ/tongue`'s var and `/atom/movable`'s proc):** action_click, activated, aim_assist, allow_buffer_activate, announce, attack, attack_hit, authenticated, autoclose, burn, can_atmos_pass, can_dismember, can_speak, can_unwrench, charge, charge_cell, completed, cooldown, countdown_step, create_modular_set, custom_fire_overlay, death_sound, delete_click, dissipate, drone_log, enter_delay, equipped, exit_delay, flush, force_open_above, get_pdas, guilty, hijack_speed, holiday_lights, implanted, invocation, ionpulse, load, malfhack, mode, on_apply, on_state, open, post_attack_hit, powered, pre_attack_hit, print_text, processing, purge_logs, query, radio, randomize, record_start, removed, reverse, say_mod, scorned_by, set_mode, set_up, shock, sig_caught, slows_projectiles, speak, stabilize, start, started, stop, toggle_ids, toggle_relay, upgrade, vend_item. No member at the snapshot has the name of its own class.

## Appendix C. Name index

Status: **exists** means implemented and tested; **exists (skeleton)** means the public signature is frozen and the body throws `NotImplementedException` naming its owner (since wave 1 only `image` and `icon`, "later: presentation"), see `docs/ai/specs/shim-ownership.md` for which package owns which file; **planned** names are contracts for a later package (Tg infrastructure: the pilot and later waves). `AppendixCNameTests` (`tests/Anomaly.Core.Tests/Contracts/`) checks that every name listed here as existing exists, and, the other way, that every public type and member of the Kernel, the shim and the Tg infrastructure (`TgState`, `TypeOrder`, `ProcOrder`, `GlobalProcs`, `Builtins`, `Defines`) occurs in a code span of this appendix, except the compiler-facing pattern members (`GetEnumerator`, `GetAwaiter`, the async builder's methods, ...), `Dm`'s stddef constants and the members of `GlobalProcs` and `Defines`: a package that adds a public name adds it here in the same commit. The skeleton names of one row all belong to one package (values or dynamic), and no values row is next to a dynamic row (git merges two branches' edits cleanly only when an unchanged line separates them), so the two packages, which build in parallel, each update the status of their own rows only and never conflict.

**C.1 Written bare** (section 1.2), DM spelling, imported with `using static`:

| Names | Class | Status |
|---|---|---|
| `TRUE`, `FALSE` (`const float`), `NORTH`, `SOUTH`, `EAST`, `WEST`, `NORTHEAST`, `NORTHWEST`, `SOUTHEAST`, `SOUTHWEST`, `UP`, `DOWN` and the other BYOND `stddef.dm` constants tg uses but does not `#define` (`src/Core/DmShim/Dm.Constants.cs`); the `world` facade (`Dm.world`, class `DmWorld`: `time`, `tick_lag`, `fps`, `tick_usage`, `cpu`, `realtime`, `timeofday`, `timezone`, `maxx`, `maxy`, `maxz`, `log` (a `DmWorldLog`), `contents`; rule 10.8; W3.0 added `map_cpu` (always 0, DV3) and the `/world` vars of `code/world.dm`, `turf`, `area`, `mob`, `name`, `view`, `params` (written `world.@params`), `system_type` (always `UNIX`) and `sleep_offline`, stored per world in `DmWorldState` and set by the Tg bootstrap; a change of `maxx`, `maxy` or `maxz` calls the map grid hook `DmWorldState.MapResized`) | `Dm` | exists |
| `istype(x, T)` and `astype(x, T)` with a `Type` value, `ispath`, `typesof`, `text2path`, `hascall`, `@ref` (BYOND `ref(x)`, which tg's `REF`/`text_ref` expand to) | `Dm` | exists (v1.2: `@ref` also gives lists, typepaths and `world` a ref, which `Dm.locate_ref` resolves, object-model.md section 3; `hascall(world, x)` sees tg's `/world` procs; W3.0: `typesof` of a proc path, `typesof("/datum/controller/global_vars/proc")`, lists in the generated proc order only the procs the binder resolves on the type, so a proc that is not translated yet does not exist) |
| `sleep`, `spawn` (forward to the Kernel's `Sleeping.Sleep` and `Waiting.Spawn`), `input`, `alert` | `Dm` | exists (`input`, `alert`: stubs completing with null until the UI milestone) |
| `prob`, `rand`, `pick` (unweighted), `roll` (world RNG) | `Dm` | exists |
| `round`, `floor`, `ceil`, `trunc`, `fract`, `sign`, `abs`, `sqrt`, `min`, `max`, `clamp`, `log`, `lerp`, `sin`, `cos`, `tan`, `arcsin`, `arccos`, `arctan` | `Dm` | exists (W3.0: `log(X, Y)` is exact when Y is an integral power of X, `log(2, 8) == 3`, which tg's `SetRunLevel` uses as a list index) |
| `length`, `length_char`, `copytext`, `findtext`, `findtextEx`, `findlasttext`, `replacetext`, `replacetextEx`, `splicetext`, `spantext`, `nonspantext`, `uppertext`, `lowertext`, `trimtext`, `splittext`, `jointext`, `sorttext`, `sorttextEx`, `cmptext`, `cmptextEx`, `ckey`, `ckeyEx`, `ascii2text`, `text2ascii`, `num2text`, `text2num`, `html_encode`, `html_decode`, `json_encode`, `json_decode`, `params2list`, `list2params`, `time2text`, `rgb`, `rgb2num` and the `_char` variants (`copytext_char`, `findtext_char`, `findtextEx_char`, `findlasttext_char`, `replacetext_char`, `replacetextEx_char`, `splicetext_char`, `spantext_char`, `nonspantext_char`, `splittext_char`, `text2ascii_char`) (`url_encode` and `url_decode` are tg macros, rule 1.6) | `Dm` | exists (a `/regex` needle, delimiter or proc replacement is a runtime error until `/regex` is ported; since v1.2 every text parameter is `object` and a value that is not text behaves as in OpenDream, mostly like null, `docs/ai/specs/dm-conformance.md`; `uppertext` and `lowertext` also have a `string` overload that returns `string`; `sorttextEx` and `cmptextEx` added) |
| `isnum`, `istext`, `islist`, `isfile`, `isicon` | `Dm` | exists (`isfile`: true for a `file()` reference, a `DmFileRef`, and for a resource, a `DmResource`; `isicon`: true for a resource of an image file, false for every other value until `/icon` is placed) |
| `Dm.resource`, `DmResource` (`Path`, `Equals`, `GetHashCode`, `ToString`): resource literals (3.7) | `Dm` | exists (wave 4, `DmResource.cs`) |
| `animate`, `flick`, `sound`, `image`, `matrix`, `icon` | `Dm` | exists (`animate`, `flick`, `sound` and the `sound` class with BYOND's `/sound` vars, `file`, `repeat`, `wait`, `channel`, `volume`, `frequency`, `offset`, `len`, `pan`, `pitch`, `priority`, `status`, `x`, `y`, `z`, `falloff`, `environment`, `echo`, `atom`, `params`, `transform`, and the port-only `port_pressure_unaffected` (DV37: playsound_local sets it per call, the server's sound feed reads it); `matrix` and the `matrix` class, components `a`-`f` (`a`, `b`, `c`, `d`, `e`, `f`), whose procs keep their DM names: the values package added `Turn`, `Scale`, `Translate`, `Multiply`, `Invert`, `Add`, `Subtract`; the necropolis loot added `Interpolate`, BYOND's linear blend of two matrices); W4.0 (lead decision D3, wave 4): `image(icon, loc, icon_state, layer, dir, pixel_x, pixel_y)` returns the minimal `image` class (`src/Core/DmShim/image.cs`, a builtin value like `sound`: no world id, hashed by its fields) with BYOND's `/image` vars `vis_contents` (silicons SI4: the objects drawn as part of the image), `icon`, `icon_state`, `loc`, `layer`, `plane`, `dir`, `pixel_x`, `pixel_y`, `appearance_flags`, `alpha`, `color`, `invisibility`, `override` (`@override`); exists (`icon`: M4c slice 4, `src/Core/DmShim/icon.cs`) |
| `get_step`, `get_dist`, `get_dir`, `get_step_towards`, `get_step_away`, `step`, `step_to`, `step_away`, `walk*`, `range`, `orange`, `view`, `oview`, `hearers`, `viewers`, `block`, `locate(x, y, z)`, `ismob`, `isturf`, `isarea`, `ismovable`, `isloc`, `dm_view_read` | `Builtins` (Tg) | skeleton signatures of `get_step(Ref, Dir)`, `get_dir`, `isturf`, `block` (two turfs, or BYOND 515's coordinates) and `locate(X, Y, Z)` exist (W3.0, `Builtin/map.builtin.cs`, bodies `Dm.NotTranslated` until the engine package of wave 3); `step(Ref, Dir, Speed)` exists (W4.0: an `async` `DmTask<object>`, 0 without calling `Move` when `get_step` gives null or `Ref.loc`, otherwise `Move`'s own value, move-chain.md E2); `range`, `orange` and `view` exist (M3); `hearers(Depth, Center)` exists (M4b audio slice 1: the mobs on turfs in the box of `Depth` whose view of that box shows the centre's turf); `get_step_towards(Ref, Trg)` exists (M4b, the extinguisher's water: `get_step(Ref, get_dir(Ref, Trg))`, OpenDream's DMStandard); `get_step_away(Ref, Trg)` exists (M5x slice 3, a blast's throw: `get_step(Ref, turn(get_dir(Ref, Trg), 180))`, OpenDream's DMStandard); `step_towards(Ref, Trg)` exists (M5x slice 5, the singularity's pull: `Ref.Move(get_step_towards(Ref, Trg), get_dir(Ref, Trg))`, OpenDream's DMStandard, 0 without a move as `step`); `viewers(Depth, Center)` exists (M5x slice 1, an explosion's flash: the list of `hearers`, the port's `view` having no light step); `oviewers(Depth, Center)` exists (M5x slice 5, the singularity's mezzer: `viewers` without the mobs on the centre's tile, as OpenDream); `oview(Dist, Center)` exists (M7 zaps, tg's `tesla_zap`: `view` without the centre's contents and its own tile, as OpenDream); `dm_view_read(view)` exists (M5x slice 1, a SHIM-GAP helper: what reading BYOND's `world.view` gives, a number for a square view of an odd side, `"15x15"` reads 7, as OpenDream's `DreamObjectWorld`; tg's `shake_the_room` does arithmetic on it); the others planned |
| `turn(Dir, Angle)` of a direction (166 tg uses: dirs, the Move chain, matrices, icons) | `Dm` | exists (`Dm.Directions.cs`: counterclockwise, angles truncated to a multiple of 45, an invalid Dir a random direction from the world RNG unless the angle is 0); `turn(Matrix, Angle)`: exists (the `object` overload: a turned copy, as `/matrix/proc/Turn` turns; M4v slice 4); `turn(Icon, Angle)`: presentation seam, planned |
| `get_step_to`, `get_step_rand`, `step_rand`, `walk_rand`, `ohearers`, `oviewers`, `bounds_dist` (BYOND map builtins tg uses, 1 to 6 sites each) | `Builtins` (Tg) | planned, with the other map builtins (M0 atom wave, M2 movement) |
| `values_sum`, `values_dot`, `values_cut_under`, `values_cut_over`, `values_product` (BYOND 516 list arithmetic; tg: `values_sum` in `surgery/bodyparts/_bodyparts.dm:337`, `values_dot` and `values_cut_under` in `atmospherics/gasmixtures/gas_mixture.dm`) | `Dm` | exists (M2 contracts, `Dm.ListMath.cs`: list order, a float total, numbers only; dm-conformance.md "M2 contracts") |
| `isinf`, `isnan` | `Dm` | planned |
| `md5`, `sha1` | `Dm` | `md5` and `sha1` exist (M6 slice 1, `Dm.Text.cs`: the blood DNA's hash; D308: `sha1`, the TTS identifier; like `file2text` and `text2file` it is also an inactive tg `#define` under `RUSTG_OVERRIDE_BUILTINS`, `__DEFINES/rust_g.dm:297`, allow-listed in `DefineClashTests` with that reason) |
| `regex(...)` and the `/regex` datum (`Find`, `Replace`, `group`, `match`, `index`, `next`, ...), `REGEX_QUOTE`, `REGEX_QUOTE_REPLACEMENT` (94 `regex(` sites) | `Dm` (the `regex` class in the shim) | exists (M6 slice 2, `DmShim/regex.cs`, after OpenDream's `DreamObjectRegex`: the vars `flags`, `group`, `index`, `match`, `name`, `next`, `text`; `Find`, `Replace` and their `_char` variants `Find_char`, `Replace_char`, the `g`, `i` and `m` flags, BYOND's `\l`, `\L` and `\_` escapes, a global proc reference as the replacement; End is exclusive as in `findtext`); the text procs of the text row still raise a runtime error for a regex needle |
| `gradient` (7 sites) | `Dm` | planned (with `rgb`) |
| `file`, `fdel`, `fexists`, `file2text`, `text2file`, `flist`, `fcopy` (file I/O: configuration, logs, savefiles) | `Dm`, over a host file seam (the core has no file system of its own) | exists (W3.0, `Dm.Files.cs`): over the world's `DmWorldState.Files`, an `IDmFileSystem` (`Exists`, `ReadText`, `WriteText`, `AppendText`, `Delete`, `Copy`, `List`): by default a per-world `DmMemoryFiles` (`Paths`), so tests never touch the disk; a host sets `DmDirectoryFiles` (`Root`) for a real directory. `DmMountedFiles` (`Prefix`) keeps one DM folder in another file system (the server's `PLAYER_SAVES` folder for tg's `data/player_saves/`, the rest in memory). `file(Path)` returns a `DmFileRef` (`Path`), which renders as its path and takes output (`Dm.output_to(F, text)`, tg's `WRITE_FILE`, appends a line); `fexists`, `fdel`, `fcopy` and `text2file` return 1 or 0, `file2text` null for a missing file. `file2text` and `text2file` are also tg `#define`s under the inactive `RUSTG_OVERRIDE_BUILTINS` (`__DEFINES/rust_g.dm:207-209`), allow-listed in `DefineClashTests`. Savefiles are not part of it |
| silicons SI4d (D326): a world's own image files and the runtime resources: `IDmFileSystem.ReadBytes` and `WriteBytes` (bytes through the file seam; a store of text keeps them as Latin-1, `DmDirectoryFiles` as real bytes), `DmPng` (`Encode`, `Decode`, `SizeOf`: 8-bit RGBA PNGs, rust-g's `dmi_create_png`), `icon.RuntimePrefix` (`rsc/`) with `icon.Width`, `Height` and `GetPixel` of an icon made from a PNG the world wrote, `DmWorldState.RuntimeResources`, `RuntimeResourceOrder` and `AddRuntimeResource` (the world's resource cache of such images, keyed `rsc/MD5.png`, DV388) | `IDmFileSystem`, `DmPng`, `icon`, `DmWorldState` | exists (`src/Core/DmShim/DmFiles.cs`, `DmPng.cs`, `icon.cs`, `DmWorldState.World.cs`) |
| silicons SI6c (D326): the game's own files for `fexists`: `DmWorldState.GameFileExists` (a predicate the host sets; the Tg layer answers from the host's sound index, `SoundLengths.Install`), so a legacy instrument's note file exists as it does in BYOND's game folder | `DmWorldState` | exists (`src/Core/DmShim/DmWorldState.World.cs`, `Dm.Files.cs`) |
| `icon_states`, `generator` and the `/generator` datum (particles) | presentation seam | planned (M3 appearance; `icon_states` needs the `.dmi` metadata of the M3 converter) |
| `filter` and the `/dm_filter` datum (M6 slice 7, D155): `Dm.filter(...)` makes one from named arguments or an arglist (an unknown `type` throws, as BYOND's runtime error); `dm_filter`'s `type`, `name`, `Vars` (its vars in order), `Get`, `Has`, `Set` (the vars `animate` and tg's `modify_filter` assign), `Types` (the kinds BYOND knows), `SameAs` (the same filter by value, as appearances compare their filters) and `Owner` (M6 slice 7b, D156: the atom or image whose filters list holds it last, which `animate(filter)` animates); `Dm.filter_list(value, owner)` (what assigning an atom's or an image's `filters` makes: null for null or an empty list, else a filters list of the value's filters, each taking the owner when one is given); `DmList.IsFilters` (a filters list, which finds and removes its filters by name); `/icon`'s `Blend`, `DrawBox` (the M7 zaps slice: with `DrawBoxFunction`, its operation's code), `MapColors` (its number forms) and `SetIntensity` (the M7 husks slice: with `MapColorsFunction`), `Crop` and `Scale` (M10x slice D4: with `CropFunction` and `ScaleFunction`; syndicate A3: `Shift` with `ShiftFunction`; syndicate G4c: `SwapColor` with `SwapColorFunction`; gaps SP1: `DirFunction`, the direction pick an `/icon` made with a `dir` keeps for the lizard features' side shots; the M7 husks slice: tg's `/icon` procs `GrayScale` and `ColorTone` are `GlobalProcs` extension methods of `icon`, `Tg/__HELPERS/icons.cs`) (kept as operations, `dm_ops`, that the client draws) and `dm_resource()` (the resource text an appearance sends for an icon: its file, or the `iconops:` recipe of protocol.md section 34.3); `Dm.fcopy_rsc(x)` (returns its argument: the port has no resource cache); `image`'s `filter_data` and `filter_cache` (M6 slice 7b, D156: tg's /datum vars as an image holds them, for tg's filter procs run on images, `Tg/datums/datum.image.cs`) | `Dm`, `dm_filter`, `DmList`, `icon` | exists (`src/Core/DmShim/dm_filter.cs`, `DmList.Filters.cs`, `icon.cs`) |
| `winset`, `winget`, `winexists`, `winclone`, `winshow`, `browse`, `browse_rsc`, `output(msg, control)`, `link`, `shell`, `stat`, `statpanel` | UI seam (presentation) | planned (M9-M10; `winget`, `winexists` and `shell` sleep in BYOND and seed the colouring, `colored-procs.md`) |
| `refcount` (tg: unit tests only) | `Dm` | planned (the port has no reference counts; a decision when tg's reference tracking tests are ported) |
| `rand_seed` (tg: `controllers/master.dm:98`, in `/datum/controller/master/New`) | `Dm` | exists (W3.0): a no-op, the world RNG is seeded by the host and reseeded every tick (D31, rules 4.13; `docs/ai/DEVIATIONS.md` DV11) |
| `shutdown` (tg: `game/world.dm:216`, the end of `/world/proc/run_performance_tests`) | host (through `DmWorld`) | planned (M1 server lifecycle) |
| `DM_VERSION`, `DM_BUILD` (BYOND's compile-time macros, 516 and 1685 as in rule 1.7; tg reads them at run time in `helper_datums/getrev.dm:82` and `subsystem/blackbox.dm:24`) | `Dm` constants | planned |
| `world` vars and procs beyond rule 10.8 that tg reads: `view`, `icon_size`, `name`, `port`, `address`, `internet_address`, `system_type`, `timezone`, `params`, `area`, `turf`, `sleep_offline`, `byond_version`, `byond_build`, `map_cpu`, `Export`, `Reboot`, `SetConfig`, `GetConfig`, `Profile`, `OpenPort` | `DmWorld` (DmShim) | `map_cpu`, `turf`, `area`, `mob`, `name`, `view`, `params`, `system_type`, `sleep_offline` exist (W3.0, the first row of C.1); the others planned, added by contracts or the lead as translated code needs them (`port`: M1 server configuration; `icon_size`: M2) |
| every `#define` | `Defines` (Tg, generated) | exists (generated by `tools/Anomaly.Tools.Defines` into `src/Core/Tg/Generated/Defines/`; lists, typepaths, resources and run-time expressions such as `TICKS` are listed in the manifest but not emitted, `docs/ai/specs/defines-converter.md`) |
| tg global procs and macros (`qdel`, `QDEL_NULL`, `QDELETED`, `QDELING`, `stack_trace`, `assertion_message`, `LAZY*`, `REF`, `url_encode`, `get_turf`, `get_area`, `CHECK_TICK`, `TICK_CHECK`, `subtypesof`, `typecacheof`, `stoplag`, `do_after`, `span_*`, `to_chat`, ...) and the accessors `GLOB`, `Master`, `SS*`, `usr`; `ASSERT` has no member: it is expanded inline at its use (11.1, 9.5) | `GlobalProcs` (Tg) | exists: `GLOB`, `usr` (typed `mob`, whose skeleton part is `Builtin/mob.builtin.cs`), `Master` (over the skeleton `controllers/master.cs` since W3.0), `SSgarbage`, `SSdcs`; W3.0 (the wave-3 contracts, `docs/ai/specs/wave3-ownership.md`) added the accessors `Failsafe` (a skeleton that never runs, DV10), `config`, `logger`, `SStimer`, `SSprocessing`, `SSfastprocess`, `SSobj`, `SStimed_actions`, `SSatoms` and those of the inert stub subsystems (`SSticker`, `SSblackbox`, `SSdbcore`, `SSchat`, `SSmapping`, `SSjob`, `SSreagents`, `SSfluids`, `SSstation`, `SSicon_smooth`), `TICK_LIMIT_RUNNING`, and skeleton signatures (bodies `Dm.NotTranslated` until their package lands) of `TICK_CHECK`, `CHECK_TICK`, `stoplag`, `MC_RUNNING`, `START_PROCESSING`, `STOP_PROCESSING`, `REALTIMEOFDAY`, `CEILING`, `ROUND_UP`, `WRAP_UP`, `WRAP_UID`, `DS2TICKS`, `TICKS2DS`, `server_timestamp`, `_addtimer`, `deltimer`, `updatetimedelay`, `log_world`, `log_game`, `log_config`, `log_mapping`, `log_qdel`, `log_runtime`, `warning`, `notice`, `message_admins`, `to_chat`, `get_turf`, the `/world` procs `change_fps`, `get_world_state_for_logging`, `_initialize_log_files` and the no-op TGS seam `TgsInitializationComplete`; the 153 `span_*` helpers that the defines converter generates; the procs and macros of the wave-2 pilot (the datum core, `qdel`, signals, callbacks, weakrefs, `stack_trace`, `REF`, the list, sort and comparator helpers, `LAZY*`, traits, components, elements); `url_encode` over `rustg_url_encode`, rust-g's encoder written in C# (`Builtin/rust_g.builtin.cs`, rule 13); W4.0 (the wave-4 contracts, `docs/ai/specs/wave4-ownership.md`) added the accessors of the inert stub subsystems `SSspatial_grid`, `SSvis_overlays`, `SSsound_tokens` and `SSair`, the translated `REVERSE_DIR`, `GET_TURF_ABOVE`, `GET_TURF_BELOW`, `isatom`, `isliving`, `ishuman`, `isobserver`, `isitem`, `ismachinery`, `is_multi_tile_object`, `string_assoc_list`, the GLOB lists of `_globalvars/lists/mobs.dm`, `mapping.dm` and `ambience.dm`, the deferred leaf `log_combat`, and skeleton signatures of the wave-4 packages' procs (`string_list`, `dir2angle`, `angle2dir`, `get_nested_locs`, `get_step_multiz`, `do_after`, ...); the M1 contracts added the accessors of the inert stubs `SSverb_manager` and `SSinput`, the GLOB lists `persistent_clients_by_ckey` and `persistent_clients`, and skeleton signatures of `log_played_names` and `_queue_verb` (owner M1 P1, `docs/ai/specs/m1-ownership.md`); planned: the other procs, macros and `SS*` (later waves) |
| tg's procs on `/world` (`push_usr`, `__HELPERS/mobs.dm:709`) | `GlobalProcs` (Tg): extension methods of `DmWorld` in the mirror file of their `.dm` (rule 1.5), written `world.push_usr(...)` | exists; a call by name on `world` (`Dm.Call(world, "push_usr", ...)`, the `arglist` form of 7.3) reaches them through the binder, which passes `world` first (v1.2) |
| tg's dcs wrapper macros `AddElement`, `RemoveElement`, `AddComponent`, `AddComponentFrom`, `LoadComponent` (`__DEFINES/dcs/helpers.dm:14-26`, calls on `src` or an explicit receiver) | non-virtual public instance methods of tg's `datum` (`__DEFINES/dcs/helpers.cs`; rules 7.3, 11.1), written `parent.AddComponent(typeof(T), a, Dm.named("b", x))`; `AddElement` returns `DmTask`, `RemoveElement` nothing, the others `DmTask<object>` | exists |

**C.2 Written qualified:**

| Names | Status |
|---|---|
| `World`, `World.Current`, `World.ReportRuntime`, `World.AllocateId`, `World.usr`, `World.Time`, `World.Step` (Kernel) | exists |
| the Kernel's host and test API (not used by translated code; waiting-model.md, "Kernel API reference"): `World.CurrentOrNull`, `World.Enter` (`World.Scope`), `World.Tick`, `World.TickLag`, `World.Fps`, `World.Seed`, `World.StepDeciseconds`, `World.AddPhase`, `World.RuntimeErrorSink`, `World.RuntimeErrorCount`, `World.ComputeStateHash`, `World.AddStateHasher` (`StateHashContributor`, `StateHasher`: `Create`, `Add`, `Value`), `World.Rng` (`Rng`: `NextFloat`, `NextInt`, `NextUInt64`, `Reseed`, `State`, `Mix64`), `World.Scheduler` (`Scheduler`: `At`, `After`, `RunDue`, `TicksFromDeciseconds`, `TicksCovering`, `MaxZeroDelayHops`, `MaxRunsPerDrain`, `Pending`, `Sequence`, `HashInto`), `WaitingException` | exists |
| `World.Objects` (`ObjectRegistry`: `Register`, `Unregister`, `TryGet`, `HoldStrongly`, `IsHeldStrongly`, `ForEachLiveInIdOrder`, `TryGetNextLive`, `Count`, `HashInto`, the registry hash of object-model.md section 8), `IStateHashable` (`HashState`, the registry's hash hook) (Kernel) | exists |
| the registry's replication seams (M1 contracts, object-model.md section 6): `ObjectRegistry.HighestId` (the id watermark of created objects) and the event `ObjectRegistry.Unregistered` (raised by `Unregister` with the removed id) (Kernel; host code only, never translated code) | exists |
| the registry's tag index (M3 I1b, dm-conformance.md: `locate(tag)` without a scan): `ObjectRegistry.SetTag` (the datum's tag setter and its id assignment call it), `ObjectRegistry.FindTagged` (the shim's `locate(tag)`), `ObjectRegistry.TagIndexIncomplete` (set by the shim when a world registers an object that keeps no index: then `locate(tag)` scans), and the shim's marker `IDmTagIndexed` (tg's `datum`) (engine code only, never translated code) | exists |
| the host's step profiler (M3 I1b): `World.Profiler` (`IStepProfiler`: `Begin`, `End`; `World.SchedulerProfileName`), timed with the host's clock around the scheduler, the phases, the MC's fires and inits and SStimer's callbacks (a `PORT(measurement)` at each Tg site); the world never reads it (D31) (host code; the MC and SStimer call it) | exists |
| `DmTask`, `DmTask<T>` (`IsCompleted`, `IsFaulted`, `Exception`, `TryGetResult`, `CompletedTask`, `FromResult`, `FromException`, `AsObjectTask`), `IDmTask` (`AsObjectTask`, for calls by name), `Waiting.Detach`, `Waiting.DetachSync`, `Waiting.NoSleep`, `Waiting.WaitforFalse`, `Waiting.Spawn`, `DotBox<T>` (`Value`), `Sleeping.Sleep`, `Sleeping.SleepTicks`, `IDmAwaiter`; the compiler-facing types `DmTaskBuilder`, `DmTaskBuilder<T>`, `DmTaskAwaiter`, `DmTaskAwaiter<T>`, `SleepAwaitable` (what `sleep` returns) and `SleepAwaiter` (Kernel) | exists (wave-0 Kernel branch; semantics in waiting-model.md) |
| `World.GetExtension<T>(Func<World, T> create)`, the per-world extension slot (for `TgState` and the shim's `DmWorldState`) (Kernel) | exists |
| `DmWorldState.TickUsage`, `DmWorldState.ChargeTick` (DV33: the virtual tick usage `world.tick_usage` reads, charged by SSair's work and counted from 0 each tick) | exists (M4b) |
| `Dm.num`, `Dm.num_or_null`, `Dm.truthy`, `Dm.not`, `Dm.eq`, `Dm.equiv`, `Dm.lt`, `Dm.le`, `Dm.gt`, `Dm.ge`, `Dm.cmp` | exists |
| `Dm.add`, `Dm.sub`, `Dm.mul`, `Dm.div`, `Dm.neg`, `Dm.mod`, `Dm.fmod`, `Dm.pow`, `Dm.inc`, `Dm.dec`, `Dm.postinc`, `Dm.preinc`, `Dm.postdec`, `Dm.predec` | exists |
| `Dm.append`, `Dm.remove`, `Dm.combine`, `Dm.mask`, `Dm.bitxor_assign` (each with a `ref` overload and a list overload without `ref`), `Dm.append_at`, `Dm.remove_at`, `Dm.combine_at`, `Dm.mask_at`, `Dm.bitxor_assign_at`, `Dm.inc_at`, `Dm.dec_at`, `Dm.postinc_at`, `Dm.preinc_at`, `Dm.postdec_at`, `Dm.predec_at`, `Dm.bitand`, `Dm.bitor`, `Dm.bitxor`, `Dm.bitnot`, `Dm.shl`, `Dm.shr` | exists |
| `Dm.@in`, `Dm.in_to` (rule 4.7; v1 named it `in_range`, a tg `#define`), `Dm.each`, `Dm.each<T>`, `Dm.each<T>()` (the loop without `in`, 6.5), `Dm.each_cast<T>`, `Dm.each_as`, `DmAs` (the input types of `as`: `anything`, `area`, `turf`, `mob`, `obj`, `text`, `num`, `null`, `icon`, `sound`, `file`, `message`, `key`, `password`, `color`, `command_text`), `Dm.pairs`, `Dm.pairs<T>` (the loop sources `DmEach<T>`, `DmPairs<T>`) | exists |
| `DmList` (indexer, `len`, `Add`, `Remove`, `RemoveAll`, `Cut`, `Copy`, `Find`, `Insert`, `Join`, `Swap`, `Splice`; `dm_owner`, the engine owner of a builtin list, told after every write through `IDmListOwner` (`ListChanged`); `dm_unique`, an engine list that holds an item once, BYOND's `client.screen`, M2 integration; `IsAlist`, DM's `istype(L, /alist)`, silicons SI4), `Dm.list`, `Dm.alist`, `Dm.kv` (`DmKeyValue`: `Key`, `Value`), `Dm.list_n`, `Dm.pick_weighted` | exists |
| `Dm.str`, `Dm.text`, `I18n.T` (class `I18n` in `Anomaly.Core.DmShim`) | exists |
| `Dm.textchar` (DM 515's `T[i]` on a text: the character at a UTF-16 position, rules 5.6; M9) | exists |
| `[DmParams]` (`Names`), `[DmPath]` (`Path`), `[NotHashed]`, `[DmGlobalProcs]` (`Type`), `[DmTypeOrder]` (`TypeOrder`, `ProcOrder`: the assembly attribute that names the generated order classes, object-model.md section 9), `[DmPortType]` (`Type`: the assembly attribute that registers a class of the port's own whose path the generated order does not list, DV584) | exists |
| `[DmInert]` (the inert mark of a generated DM type, D112: written only by the types converter, `docs/ai/specs/types-converter.md` 4.3; M3 P1) | exists |
| `Dm.@new`, `Dm.@new<T>`, `Dm.new_async`, `Dm.named` (`DmNamedArg`: `Name`, `Value`), `Dm.arglist` (`DmArgList`: `List`), `Dm.Call`, `Dm.arg`, `Dm.arg<T>`, `Dm.forward`, `Dm.args`, `Dm.initial`, `Dm.get_var`, `Dm.set_var`, `Dm.get_var_or_null` (rule 4.8; v1 named them `get`, `set`, `get_or_null`, and tg has a global proc `get`), `Dm.has_var` (syndicate A3: `name in x.vars`, rule 10.7), `Dm.locate<T>`, `Dm.locate_type` (`locate(T) in C` with a typepath value `T`, rule 10.4; M2 integration), `Dm.locate_ref`, `Dm.parent_type`, `DmTypes` (`PathOf`, `FromPath`, `ParentOf`, `Subtypes`, `TreeOrdinal`; the dynamic package added `IsSubtypeOrSame`, DM derivation as `ispath` tests it), `DmArgs` (the bound arguments of the pre-New hook: `Positional`, `Named`, `TryGetNamed`, `Empty`; the dynamic package added `InitialLocation`, the atom's initial location: the first positional argument, or the one named `loc`), `IDmObject` (`dm_id`, `dm_assign_id`, `dm_pre_new`), `IDmAtom` (`contents`, `dm_each_contents`, what a loop over the atom walks, an area adding what stands on its turfs; `name`, `gender`; `dm_world_order`, the group of the world's order of atoms, mobs then movables then areas then turfs, added in M3 after P4 found tg relies on it) | exists |
| `TypeOrder.Paths`, `TypeOrder.Count`, `ProcOrder.Procs`, `ProcOrder.Verbs`, `ProcOrder.ProcsOf` (Tg, generated by `tools/coloring` into `src/Core/Tg/Generated/`; the DM object-tree order and `typesof(/t/proc)`, object-model.md section 9) | exists |
| the pre-New hook override `protected internal override void dm_init_vars()` (10.2) of tg's `datum` | exists (`Builtin/datum.builtin.cs` declares it; overrides in `garbage.cs`, `dcs.cs`, `sort_instance.cs`) |
| `Dm.CRASH`, `Dm.EXCEPTION`, `Dm.exception` (the `exception` datum: `name`, `desc`, `file`, `line`), `Dm.throw_value`, `Dm.NotTranslated`, `Dm.Deferred` (rules 1.4: a leaf deliberately skipped in the current milestone; it never throws, counts per world in `DmWorldState.DeferredCount` and `DmWorldState.DeferredSites` and logs its first call per site to the host log; every use is listed in `docs/ai/specs/not-translated.md`); `DmRuntimeException` (the base of every DM runtime error; `DmFile`, `DmLine`), `DmCrashException`, `DmThrowException` (`Value`, the thrown DM value), `DmNotTranslatedException` (`Where`, `What`) | exists (`Dm.Deferred`: W3.0) |
| `Dm.output_to` (DM `A << B`, rules 12; v1 named it `Dm.output`, BYOND's `output()` builtin) and the sink interfaces (`IMessageSink`: `Chat`, `Output`, `Balloon`, `Runechat`, `SayModal` and `Tts` (M9: runechat's finished line, DV160, and tg's say window messages; D307: a line the listener's client voices, DV262); `ISoundSink`: `Play`; `IAppearanceSink`: `Changed`, `Animate`, `AnimateStep` (M4v: the step's subject and the vars before it; defaults to `Animate`), `Flick`, `LightChanged`, `VisibilityChanged`; `AppearanceAspect`: `Icon`, `IconState`, `Dir`, `Layer`, `Plane`, `Alpha`, `Color`, `Pixel`, `Transform`, `Overlays`, `Underlays`, `VisContents`, `Maptext`, `AppearanceFlags`, `BlendMode`, `Filters`, `Render`, `Glide`; `PresentationSinks`: `Of`, `Messages`, `Sounds`, `Appearance`, `Replication`, `Reportable`; `NullSinks`; `IReplicationHintSink` (M1 contracts: `Moved`, `AppearanceChanged`, `VisibilityChanged`, `Deleted`; the per-world replication hints, presentation-seam.md rule 5, object-model.md section 6; null unless a host installs one); `RecordingSinks`: `Install`, `Lines`, `Describe`; presentation-seam.md), the shim's per-world state `DmWorldState` (`Of`, `Current`, `Sinks`, `WorldLog`, `RebootRequested` (M10 slice H: BYOND's `world/Reboot`, the host's restart), `NotTranslatedThrows`, `RealtimeStart`, `maxx`, `maxy`, `maxz`; W3.0: the storage of the `/world` vars `turf`, `area`, `mob`, `name`, `view`, `params`, `system_type`, `sleep_offline`, the map grid hook `MapResized` (called with the old maxx, maxy, maxz after a change through the facade), the file seam `Files`, the `Dm.Deferred` counters `DeferredCount` and `DeferredSites`) | exists |
| attributes `[ShouldCallParent]`, `[ShouldNotSleep]`, `[SignalHandler]`, `[ShouldNotOverride]`, `[ShouldBePure]`, `[DmProtected]`, `[DmPrivate]`, `[DmFinal]` | exists |
| `TgState`, `TgState.Current`, `TgState.Of`, `TgState.InitStatics` (Tg, `src/Core/Tg/Infrastructure/`) | exists |
| `TgWorld.Boot` (Tg, `src/Core/Tg/Infrastructure/TgWorld.cs`: the world bootstrap, map grid, `/world` defaults, static initialization with Genesis first, the compiled-map stand-in, then `world/New` detached) | exists (wave 3) |
| `ToyRoom`, `ToyRoom.Build`, `ToyRoom.MinSize`, `ToyRoom.DefaultSize`, `ToyRoom.FloorBottomLeft`, `ToyRoom.FloorTopRight` (Tg, `src/Core/Tg/Infrastructure/ToyRoom.cs`: host code, not tg code; the toy room of wave 4's W4.M and of the wave 5 toy world, built from tg's real types in the current world with no map file: `world.turf` set to `/turf/open/indestructible` before the map grows, `/turf/closed/indestructible` walls over the rim, SSmapping's per-z lists grown to `world.maxz`, one `/area/misc/testroom` with a null location and tg's `change_area` per turf; `FloorBottomLeft` and `FloorTopRight` are tg's `run_loc_floor_bottom_left` and `run_loc_floor_top_right`; usable as the `compiledMap` step of `TgWorld.Boot` and in a test world that does not boot) | exists (wave 4, W4.M) |
| `ToyScenario`, `ToyScenario.Run`, `ToyScenario.DefaultSeed`, `ToyScenario.Seed`, `ToyScenario.World`, `ToyScenario.StartTick`, `ToyScenario.Log`, `ToyScenario.Frames`, `ToyScenario.CompletedDoAfter`, `ToyScenario.CancelledDoAfter`, `ToyScenario.Runtimes`, `ToyScenario.StateHash`, the script's tick offsets `ToyScenario.FirstStepAt`, `SecondStepAt`, `WallStepAt`, `FirstDoAfterAt`, `SecondDoAfterAt`, `CancelStepAt`, `EndAt`, `DoAfterTicks`, and its records `ToyLogEntry` (`Tick`, `Name`, `Detail`), `ToyFrame` (`Tick`, `Caption`, `Rows`, `MobDir`) and `ToyDoAfter` (`Number`, `StartTick`, `EndTick`, `Done`, `Result`, `Succeeded`) (Tg, `src/Core/Tg/Infrastructure/ToyScenario.cs`: host code, not tg code; the scripted M0 scenario of ROADMAP M0's "Done when", run in a new `World` of a seed: `TgWorld.Boot` with `ToyRoom.Build` as the compiled map, the world stepped until the real Master has initialized and settled (tick 30), RUNLEVEL_GAME set, a plain `/mob` and a plain `/obj` placed, then six player commands from the player command phase on fixed ticks: `step` east twice, `step` south into the wall (Bump), a 1 second `do_after` that completes (TRUE), a second `do_after` and a `step` 5 ticks later that cancels it (FALSE). It records the log of the commands and of the signals tg sends to a plain listener datum (`COMSIG_MOVABLE_PRE_MOVE`, `COMSIG_MOVABLE_MOVED`, `COMSIG_MOVABLE_BUMP`, `COMSIG_DO_AFTER_BEGAN`, `COMSIG_DO_AFTER_ENDED`), the results and ticks of both do_afters, an ASCII frame of the room before the first command and after each, and `World.ComputeStateHash()` at the end; two runs of one seed are identical) and `Anomaly.Tools.ToyWorld` (`tools/Anomaly.Tools.ToyWorld`: `Program`, `ToyWorldRunner`, `ToyWorldRunner.Usage`, `ToyWorldRunner.Run`: the console program that prints the frames, the log and the final hash, `--seed N`, `--twice`; never used by translated code) | exists (wave 5, W5.A) |
| the M1 host seams of the Tg infrastructure (host code, not tg code; never used by translated code except the `/client` engine part's two notify calls; owners in `docs/ai/specs/m1-ownership.md`): `MapView` (`Width`, `Height`, `Levels`, `TurfAt`: the read-only map for a host outside the world, object-model.md section 6.1); the client seam `ClientSeam` (engine to host: `SetHost`, `HostOf`, `NotifyClientDeleted`, `NotifyClientMobChanged`; host to engine: `KeyMob`, `Connect`, `SetHeldDirections`, `Disconnect`, and `MovementHotkey`, the default movement hotkey of a direction that `SetHeldDirections` sends), `IClientHost` (`ClientDeleted`, `ClientMobChanged`) and `ClientConnection` (`Client`, `Done`, `Mob`, `Error`, `Accepted`); the `/client` engine part's public members for hosts and tests (`client.dm_connected`, `client.dm_gone`, `client.EngineByondVersion`, `client.EngineByondBuild`, `Builtin/client.builtin.cs`); the M1 test room `TestRoom` (`Build`, `DefaultSize`, `MaxSize`, `MinSize`, `FeaturedSize`, `IsWall`, `Area`, `SpawnPoints`) and `SpawnPoint` (`X`, `Y`, `Z`); `ToyScenario.Run`'s `prepare` hook | exists: `MapView`, `ClientSeam`, `IClientHost`, `ClientConnection`, `ToyScenario.Run`'s hook (M1 contracts); the host entry points, `MovementHotkey` and the `/client` engine members (M1 P1); `TestRoom` (M1 P3) |
| the host programs (never used by translated code; owner: apps, `docs/ai/specs/apps.md`): `Anomaly.Server` (M1 P3: `Program`, `ConsoleInput`, `ServerHost`, `ServerConfig`, `RunResult`, `Session`, `SessionState`, `QueuedCommand`, `CommandKind`, `CommandQueue`, `ConsoleCommand`, `ConsoleVerb`, `CommandLog`, `ServerMetrics`, `LatencyHistogram`, `TickSample`, `ITickPacer`, `FastForwardPacer`, `RealtimePacer`, `SmokeLoad`, `IServerCodec`, `ProtocolCodec`, `IClientEntryPoints`, `ILoginHandle`, `TgClientEntryPoints`, `NoReplicator`; M0's `ServerOptions` and `ServerLoop` are gone), `Anomaly.Server.Replication` (`IReplicator`, `DeltaFrame`, `JoinMessages`, `ReplicationStats` by the M1 contracts; M1 P4's `Replicator`, `ReplicatorOptions`, `IReplicationEncoder`, `CodecReplicationEncoder`, `ReplicatedFields`, `FieldSource`, `ReplicationDetail`, `SweepCatch`), the protocol and transports (`Anomaly.Protocol`, `Anomaly.Net`, M1 P2, `docs/ai/specs/protocol.md`), the bots (`Anomaly.Tools.Bots`, M1 P5) and the Godot client (`godot/`, M1 P6; M0's `Anomaly.Client.Main` is gone) | exists |
| CS0252, CS0253, CS0472 as errors in `Anomaly.Core.Tg` (build) | exists (`src/Core/Tg/Anomaly.Core.Tg.csproj`) |
| the M2 appearance holders (M2 contracts, `docs/ai/specs/m2-ownership.md`): `IDmAppearance` (`icon`, `icon_state`, `dir`, `layer`, `plane`, `alpha`, `color`, `pixel_x`, `pixel_y`, `pixel_w`, `pixel_z`, `transform`, `appearance_flags`, `blend_mode`, `invisibility`, `overlays`, `underlays`, and since protocol version 18 `maptext`, `maptext_width`, `maptext_height`, `maptext_x`, `maptext_y` on `IDmAppearance`, `DmAppearance` and `image` (the HUD's maptext): the read-only appearance of an atom, an image, a mutable appearance or an overlay snapshot, which the server's appearance interning reads); the rest of `image`'s BYOND vars (`pixel_w`, `pixel_z`, `transform`, `blend_mode`, `overlays`, `underlays`, `name`, `desc`, `maptext`, `mouse_opacity`, `render_source`, `render_target`, `vis_flags`, `filters`, and since the M7 tool kit `appearance`, BYOND's appearance var, which the T-ray scan sets to copy a mutable appearance onto the image); `mutable_appearance` (BYOND's `/mutable_appearance`, a subclass of `image`: `New` copies a source's appearance and applies tg's override, `datums/mutable_appearance.dm:8`; `density`, `gender`, `screen_loc`); `icon` (BYOND's `/icon`, which M2 signatures name; M4c slice 4: `Dm.icon(icon, state, dir, frame, moving)` makes one from an icon file or another icon, kept as `dm_file`, `dm_state`, `dm_dir`, `dm_frame`, and an appearance shows that file's state); `AppearanceAspect.Name` (the `name` engine property of `/atom`) | exists (`src/Core/DmShim/IDmAppearance.cs`, `image.cs`, `mutable_appearance.cs`, `icon.cs`; the overlay snapshot that `overlays` makes of an added item is M2 P3's) |
| the overlay snapshot (M2 P3, `docs/ai/specs/dm-conformance.md` "M2 P3"): `DmAppearance` (an interned, immutable `IDmAppearance`: `Of(source)`, the per-world snapshot of an appearance; `OverlayItem(item)` and the delegate `OverlayConverter`, the conversion of an item added to or looked up in an atom's `overlays` or `underlays`; `InheritsDir`; the BYOND vars `icon`, `icon_state`, `dir`, `layer`, `plane`, `alpha`, `color`, `pixel_x`, `pixel_y`, `pixel_w`, `pixel_z`, `transform`, `appearance_flags`, `blend_mode`, `invisibility`, `overlays`, `underlays`, `name`, `desc`, `maptext`, `mouse_opacity`, `render_source`, `render_target`, `vis_flags`, `filters`, `density`, `gender`, `screen_loc`, read-only); `DmList.dm_convert` (an engine list's item conversion, installed on `/atom`'s `overlays` and `underlays` by `Tg/Builtin/atom.overlays.builtin.cs`) | exists (`src/Core/DmShim/Appearance.cs`, `Appearance.World.cs`, the hook in `DmList.cs`) |
