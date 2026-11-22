# Coloured procs: which C# slots are async

Status: generated for the whole tg tree by `tools/coloring/color_procs.py` and committed (waiting-model.md v1 section 4.3: colouring cannot be decided file by file, so it is computed once for the whole tree). The translation reads it as input; it is not maintained by hand as files are translated. Regenerate it when the tg snapshot changes. The waiting model that consumes it is `docs/ai/specs/waiting-model.md` (v1); the slot registry and the type order are inputs of `docs/ai/translation-rules.md` (v1) 7.1 and of `docs/ai/specs/object-model.md` section 9.

Generated data (one file per question a translator asks):

| File | One row per | Answers |
|---|---|---|
| `docs/ai/specs/generated/colored-slots.tsv` | slot (root type path + proc name) | May a definition of this slot sleep (`colored`)? Must its callers wait (`suspends`: the C# signature is `DmTask`)? Is it `waitfor = FALSE` or sync-mandated? Why (witness chain)? |
| `docs/ai/specs/generated/nosleep-sites.tsv` | call site in a synchronous C# method that calls a slot that suspends | Where `Waiting.NoSleep(...)` is written, and whether the callee can really suspend there (a conflict) or always completes |
| `docs/ai/specs/generated/sleeping-new.tsv` | `New` override that may sleep | Which `new T(...)` can suspend its caller (`Dm.new_async`, translation-rules.md 10.1) |
| `docs/ai/specs/generated/slots.tsv` | slot with tg definitions | The slot's C# signature (translation-rules.md 7.1 and Appendix A): method name, parameter names and types, `__extra`, return type, `DmTask` or not, and why |
| `src/Core/Tg/Generated/TypeOrder.g.cs` | DM type path | The DM object-tree order of every type (object-model.md section 9), for `typesof`, `subtypesof` and the type registry |
| `src/Core/Tg/Generated/ProcOrder.g.cs` | type that tg enumerates with `typesof(/t/proc)` | The procs declared on it in definition order (`GLOB`'s `InitGlobal*` procs) |

The sections marked "generated" below are rewritten by the tool; everything else is hand-written.

## Terms

- **Root declaration**: the topmost definition of a proc name on a type path, where no ancestor type defines the name. Builtin procs count as declarations on their builtin type (`/datum/New`, `/atom/Entered`, `/atom/movable/Move`, `/atom/movable/Bump`, `/mob/Login`, ...). The builtin root types `/datum`, `/client`, `/world`, `/list`, `/savefile` (and `/alist`, `/vector`, `/callee`) stop the walk: tg sets `/client parent_type = /datum` (client_defines.dm), but `/client/New` is still a slot of its own, separate from `/datum/New`.
- **Slot**: (root type path, name). One slot is one C# virtual hierarchy: the root declaration and every override below it share one signature. Global procs are slots with the root `/proc`. Unrelated roots with the same name (`/atom/Initialize`, `/datum/controller/Initialize`, `/datum/component/Initialize`, ...) are independent.
- **May sleep** (per definition): its own code, outside detached `spawn`/`ASYNC` bodies, calls a sleep primitive, makes a call that can suspend it, or calls `..()` into a parent that can suspend it.
- **Coloured slot** (`colored = yes`): some definition in it may sleep.
- **Suspends** (`suspends = yes`; "callers must wait" in waiting-model.md 4.2): some definition that may sleep has an effective `waitfor = TRUE`, so a call can suspend the caller. Every slot that suspends is coloured. A coloured slot with `suspends = no` is one whose sleeping definitions are all `waitfor = FALSE`.
- **Sync-mandated**: `SIGNAL_HANDLER` or `SHOULD_NOT_SLEEP(TRUE)`, on the definition or inherited from its parent chain (SpacemanDMM: "This cannot be disabled by child overrides"; `SIGNAL_HANDLER` is `SHOULD_NOT_SLEEP(TRUE)`, dcs/helpers.dm). Such a definition never sleeps by tg's lint and is never coloured; its calls into slots that suspend are `Waiting.NoSleep` sites (waiting-model.md 4.4).
- **waitfor**: `set waitfor = FALSE` is a property of the callee, inherited by overrides that do not set it (OpenDream `DMCodeTree.Procs.cs` copies it to an override from the first definition at the nearest type that defines the proc; the scanner copies it from the parent definition, which is the same for the snapshot because no tg type has several definitions of a proc that differ in `waitfor`; tg never sets `waitfor = TRUE` back).

## The rule: colour is per slot (waiting-model.md v1 section 4.2)

A slot is one C# virtual method, so its signature is decided once for the root and every override, from the columns `colored` and `suspends` of `colored-slots.tsv`:

| `colored` | `suspends` | C# signature of the root and every override | Definitions that may sleep |
|---|---|---|---|
| yes | yes | `DmTask` / `DmTask<T>`, whether or not this definition sleeps itself; every call into the slot is awaited or uses a form of waiting-model.md section 5 | `waitfor = TRUE` ones are `async` and awaited; a `waitfor = FALSE` one gets the 5.4 wrapper, made `async` so it fits the signature, and returns a completed task |
| yes | no | synchronous (`void` / `T`): callers never wait (`/datum/process`, `/datum/controller/subsystem/ignite`, `/datum/callback/InvokeAsync`, `/atom/Bumped`, `/atom/LateInitialize`) | all are `waitfor = FALSE` (own or inherited) and get the wrapper and `__body` pair of waiting-model.md 5.4: a plain method that returns `.` as it was at the first sleep |
| no | no | synchronous | none |

`slots.tsv` applies this table: its `signature` column has `DmTask` exactly when `suspends = yes`. Translators never colour by hand and never by what one definition does.

## How a translator uses it

1. Find the slot of the proc being translated (its root type path and name; for an override, the root it overrides) in `slots.tsv` and `colored-slots.tsv`.
   - The slot is `DmTask` / `DmTask<object>` exactly when `slots.tsv` says so (`suspends = yes`; its `signature` column already has that form). A slot that is coloured only through `waitfor = FALSE` definitions (`colored = yes`, `suspends = no`) keeps its synchronous signature, and those definitions get the callee-side wrapper of waiting-model.md 5.4. Neither a row in `colored-slots.tsv` (every slot has one) nor `colored = yes` alone makes a slot async.
   - Copy the signature from `slots.tsv` (`signature`: the root's declaration, built from `cs_name`, the parameters, `variadic`, `returns` and `suspends`) for the root and every override (an override is `public override` with the same parameters). An override whose DM declaration names fewer, other or more parameters than the slot's carries `[DmParams]` (`[DmParams()]` when it declares none) and unpacks its extra parameters from `__extra` (translation-rules.md 7.2; column `dmparams_overrides` counts them). A parameter listed in the `undecided` column keeps its declared type; the root's translator decides it by reading the listed site, records a change in translation-rules.md Appendix A in the same commit and reports it to the lead, who updates the scanner or the rules (translation-rules.md 3.3 P1, 7.1).
   - `waitfor_false = yes`: the definitions that may sleep get the callee-side wrapper of waiting-model.md 5.4. `some`: only the overrides that set (or inherit) `waitfor = FALSE` get it; check the override chain.
   - `sync_mandated = yes`: the body never awaits; it calls slots that suspend through `Waiting.NoSleep`. In a slot that suspends it still returns a (completed) `DmTask`. `some`: only some overrides are sync-mandated.
2. At a call site:
   - inside an async method (a definition in a slot that suspends, not sync-mandated, or the `__body` of a 5.4 wrapper): `await` every call into a slot that suspends (awaiting a completed task costs nothing); a call into any other slot is a plain call;
   - inside a synchronous method (sync-mandated, or a definition of a slot that does not suspend and is not a `__body`): a call into a slot that suspends is `Waiting.NoSleep(Foo(...))`. Every such site is a row of `nosleep-sites.tsv`; `may_suspend = yes` marks the real conflicts, where tg relies on a runtime condition (`force = TRUE`, a flag) or on its lint, and `NoSleep` reports a runtime if the callee does suspend. A call into a slot that does not suspend (for example `queue_node.ignite(...)`, master.dm:902) is a plain call and has no row;
   - detached calls (`INVOKE_ASYNC`, `ASYNC`, `spawn`) are `Waiting.Detach(Foo(...))` (`Waiting.DetachSync` for a synchronous callee) and never colour the caller.
3. `new T(...)`: check `sleeping-new.tsv` for the `New` that `T` reaches; `new` of a computed type reaches any of them. Inside an async proc, a `new` whose `New` has `suspends = yes` there, or of a computed type, is `await Dm.new_async<T>(...)` / `await Dm.new_async(T, ...)`; every other `new`, and every `new` in a synchronous proc, is the synchronous `Dm.@new` (translation-rules.md 10.1, object-model.md section 4). The `new` rows of `nosleep-sites.tsv` with `may_suspend = yes` are the creations of such types from synchronous code (DV5).

## How to run

From the repository root (Python 3, standard library only):

```sh
python3 tools/coloring/color_procs.py          # regenerate docs/ai/specs/generated/*, src/Core/Tg/Generated/*.g.cs and the generated sections below
python3 tools/coloring/color_procs.py --check  # exit 1 if the committed files are stale
python3 tools/coloring/color_procs.py --stats  # print the summary only
python3 tools/coloring/selfcheck.py            # assert known facts (under a minute)
```

The tg clone is found at `$ANOMALY_TGSTATION`, else at `../tgstation` next to the repository (or next to the main checkout when running from a git worktree); `--tg PATH` overrides it. A run takes about 20 s (13 s parsing, 4 s for the colouring and its comparison run, 2 s for the slot registry and the type order). Output is byte-identical across runs and `PYTHONHASHSEED` values. `--bound lower` prints the lower-bound numbers, and `--define UNIT_TESTS` (any `NAME[=VALUE]`) the numbers of a build variant, without writing anything.

## Method

1. **Files.** `tgstation.dme` is walked in include order, recursively (`.dme` includes too). Comments and string contents are blanked first (block comments nest; `{"..."}` and `@"..."` strings; embedded `[expressions]` inside strings are kept, because they are code). Line numbers are preserved.
2. **Preprocessor.** `#define`/`#undef` (object-like and function-like macros, `#`, `##`, variadic), `#if`/`#ifdef`/`#ifndef`/`#elif`/`#else` are evaluated with the macro table as it evolves, with `DM_VERSION 516`, `DM_BUILD 1685` (tg's `dependencies.sh`), `CBT` (tg's build passes it to every compile, `tools/build/build.ts:229`), tg's `DEBUG` (set in the `.dme`) and `DISABLE_DREAMLUAU` (translation-rules.md 1.7: tg defines it for OpenDream at `_compile_options.dm:133`; it removes the Lua admin scripting, which is not ported). `UNIT_TESTS`, `TESTING` and `SPACEMAN_DMM` are not defined: this is tg's production build plus `DISABLE_DREAMLUAU`, the configuration that translation-rules.md 1.7 makes binding for the translation. Macros are expanded in proc bodies, so `UNTIL`, `SLEEP_CHECK_DEATH`, `SLEEP_NOT_DEL`, `INVOKE_ASYNC`, `ASYNC`, `CALLBACK`, `SEND_SIGNAL`, `PROC_REF` and `SUBSYSTEM_DEF` are seen as what they expand to. Six names are never expanded: `SIGNAL_HANDLER` and `SHOULD_NOT_SLEEP` (they expand to nothing outside SpacemanDMM; the scanner reads them), `CHECK_TICK`/`CHECK_TICK_HIGH_PRIORITY`, which never yield in the port, and `TICK_CHECK`/`TICK_CHECK_HIGH_PRIORITY`, which are always false there (translation-rules.md 11.3 and `docs/ai/DEVIATIONS.md` DV3: tick usage is virtual and always 0).
3. **Parser.** Logical lines (open brackets and trailing `\` join physical lines) are split into the DM block structure: type blocks by indentation or braces, `var` declarations (with their declared types), proc and verb definitions with their owning type path, and proc bodies (indentation or brace-style, including macro-generated one-line `/t/proc/x(){...}` bodies). Items that fit no pattern are counted as unparsed (the summary shows the count).
4. **Body events.** For each definition, outside detached code (`spawn(...)` bodies, single statements or blocks; `ASYNC` and `INVOKE_ASYNC` expand to `spawn(-1)`) and outside `if(TICK_CHECK)` bodies and the rest of a block after `if(!TICK_CHECK) continue` (or `break`, `return`), which never run in the port (`MAPLOADING_CHECK_TICK` in the map reader, the unrolled `CHECK_TICK` of `SSatoms.InitializeAtoms` and of `SSlighting.fire`): calls with their receiver chain (`foo()`, `src.foo()`, `a.b.foo()`, `a:foo()`, `call(obj, PROC_REF(foo))()`, `call(obj, /t::foo())()`), `..()`, `new` (literal path, implicit `var/T/x = new`, or a computed path), `call()()` with a computed target, `set waitfor`, `SIGNAL_HANDLER`, `SHOULD_NOT_SLEEP(TRUE)`, every declaration of a local with its line (typed or not) and the parameters, loop variables over `typesof`/`subtypesof(/t)`, and the handler named in `RegisterSignal`.
5. **Roots and slots** as defined above. An override without any declaration in its ancestry (a parse gap) would become an implicit root; the summary counts them (0 for the snapshot).
6. **Seeds** (definitions that sleep directly): calls of the builtin sleeping procs `sleep`, `input`, `alert`, `shell`, `winexists`, `winget`, and the builtin type procs `world.Export`/`world.Import` and the hub procs of `/world`, `client.MeasureText`, `client.SoundQuery`, `client.RenderIcon`; plus `call()()` with a target the scanner cannot know (upper bound only: it may reach any proc). The builtin list contains SpacemanDMM's list of blocking builtins (`dreamchecker`: `sleep`, `alert`, `shell`, `winexists`, `winget`, `input`, `client.SoundQuery`, `client.MeasureText`, `world.Import`, `world.Export`). `set background = TRUE`, which makes a proc sleep periodically (BYOND reference), and `call_ext()` of a `byond,await:` function are not used in the snapshot. tg's own waiting helpers (`stoplag`, `UNTIL`, `do_after`, `tgui_alert`/`tgui_input_*`, `SLEEP_CHECK_DEATH`, database and HTTP waits, ...) are found by propagation from these; the generated table below lists the ones that sleep directly.
7. **Call resolution.** Each call site resolves to target sets:
   - unqualified `foo()` in a proc of type T: the slot visible at T, targets = the definition reached at T plus every override on a subtype of T (`virtual`); a global proc if no proc of T has the name;
   - `x.foo()` with a statically typed receiver (typed local, parameter, var, `src`, `usr`, `world`, `GLOB.x`, `SSfoo`, chains like `user.client.foo()`): the same, at the receiver's type; if the type has no such proc but subtypes do, every slot below it;
   - untyped or unresolvable receivers (`x.foo()` with an untyped `x`, `a[i].foo()`, `foo().bar()`, `a:foo()` on a type without it; a `:` that closes a ternary, as in `cond ? a : foo()` or the `UNTYPED_LIST_ADD` expansion, is not a member access): the union of all slots named `foo` with all their definitions (the upper bound); when only one slot has that name, the call reaches every definition of that slot, as a call through its root type would, and counts as resolved for the lower bound;
   - `new /t()` and implicit `new`: exactly the `New` reached at `/t` (no subtypes); `new expr()`: the `New` of `/t` and its subtypes when tg gives a static bound (`x.type`, a typed path var, a loop over `subtypesof(/t)`, a typed assignment target), else any `New` (union);
   - `..()`: the parent definition (the previous definition at the same type, else the nearest ancestor's);
   - `call(obj, /t::foo())(...)`: exactly the definition of `foo` visible at `/t` (`/t::foo()` is a reference to the proc of type `/t`, BYOND reference, `::` operator; tg uses it in `concrete_walls.dm` and `wall_support.dm` to run another type's implementation);
   - `step()`/`step_to()`/...: `/atom/movable/Move`; the builtin `Move` calls `Exit`/`Enter`/`Uncross`/`Cross`/`Exited`/`Entered`/`Uncrossed`/`Crossed`/`Bump`; the builtin `/client` procs that a tg override reaches through `..()` call what the BYOND reference gives as their default action: `New` calls `/mob/Login`, `Del` calls `/mob/Logout`, `Click`, `DblClick` and the `Mouse*` procs call the same proc of the atom, `Topic` calls the `Topic` of `hsrc`, `Move` calls `mob.Move`, `North` and the other direction procs call `Move`, `Stat` calls `statobj.Stat`;
   - `call()()` with a computed target resolved by tg convention: `GLOB` initialisation (`call(src, I)()` over `typesof(/datum/controller/global_vars/proc)`, the `InitGlobal*` procs), comparators of the sort and heap helpers (`call(cmp)()`, the global `cmp_*` procs), `call(src, __PROC__)()` (itself), and `_SendSignal` (registered signal handlers: they are checked through the `RegisterSignal` rows of `nosleep-sites.tsv` instead).
8. **Propagation.** Breadth-first from the seeds, so each witness chain is a shortest one. A definition that may sleep and has an effective `waitfor = TRUE` colours every caller whose target set contains it, and every override that calls `..()` into it. Nothing propagates through a `waitfor = FALSE` definition (its caller gets `.` at the first sleep), into a sync-mandated definition, or out of detached code (`spawn`, `ASYNC`, `INVOKE_ASYNC`). Creating a callback (`CALLBACK`) or a timer (`addtimer`) is not a call of its target. Phase 1 starts from the primitives and uses statically resolved call sites only (the lower bound); phase 2 adds the union sites and the `call()()` sites with an unknown target and continues to the upper bound. The committed list is the upper bound; `lower_bound = yes` marks slots that are coloured without any union site, and their witness chains contain no union site.
   **Dispatch narrowing.** A call reaches only the overrides that its receiver's static type can dispatch to (for `src`, the caller's own type), not every definition of the callee slot. The slot still decides the C# signature: every override of a slot that suspends returns `DmTask`. Narrowing decides only whether a caller is coloured by the call, or calls it with `Waiting.NoSleep` that always completes. It holds in the port because C# receiver types follow the DM declarations. The summary below also gives the numbers without narrowing (every call reaching every definition of the callee slot), for comparison.
9. **Outputs.** Rows are sorted; `colored-slots.tsv` has one row per slot that has tg definitions or call sites. Witness chains read `A (file:line) -> B (file:line) -> ... -> primitive`: each hop is a definition and the line in it that makes the next call (`..()` marks a parent call, `(builtin)` a builtin proc). In `colored-slots.tsv` a chain stops at the first later hop `D` that starts the witness of another slot `T`, written `D [T]`: the chain goes on as the witness in the row of `T`, which starts with `D` (chains are shortest paths, so following the references always ends at a primitive; most chains share a few long tails, and this halves the file). The tables in this document and `sleeping-new.tsv` show whole chains.

### Columns

`colored-slots.tsv`: `root_path`, `name`, `colored` (yes/no), `waitfor_false` (yes/some/no over the slot's tg definitions, effective value), `sync_mandated` (yes/some/no, effective value), `defs` (tg definitions), `call_sites` (named and `new` call sites resolved to this slot through a typed receiver or `src`), `witness` (for coloured slots: a chain from a definition of the slot that suspends its caller, or else from one that sleeps, to a primitive, or to `D [T]`: continued in the row of slot `T`), `suspends` (yes/no), `lower_bound` (coloured already in phase 1), `union_sites` (untyped or ambiguous sites that include this slot among their candidates), `sleeping_defs`.

`nosleep-sites.tsv`: `site` (file:line), `caller` (definition), `call` (the called name, `new`, `..()`, `call()()`, a primitive, or `RegisterSignal <handler>`), `callee` (the callee slot or slots that suspend; for `..()` the parent definition), `may_suspend` (yes: some target can suspend the caller; no: every target completes synchronously and `NoSleep` only adapts the signature), `context` (`SIGNAL_HANDLER` or `SHOULD_NOT_SLEEP`: a sync-mandated caller, own or inherited; `sync`: a caller whose C# method is synchronous by the rule above, that is a definition that does not sleep in a slot that does not suspend; `RegisterSignal`: a slot that suspends registered as a signal handler, which `_SendSignal` calls synchronously). A caller in a slot that suspends, or a definition that sleeps with `waitfor = FALSE` (its `__body` is async), awaits instead and has no rows.

`sleeping-new.tsv`: `type`, `definition` (file:line of the `New`), `suspends` (its effective waitfor is TRUE), `lower_bound`, `witness`.

## Limits

- **Regex front end, not a compiler.** Its counters of unparsed type-level items, unclosed brace blocks and overrides without a declaration are all zero for the snapshot, but a misread proc body would not show up in them, and it does not type-check: a declared var type is trusted (DM does not enforce it at run time; the C# port will, through casts), a local is typed by its declaration in scope at the use (the most recent declaration of that name at or before the line: tg reuses names such as `var/mob/living/potential_tinder` and `var/obj/item/potential_tinder` in sibling loops, and an untyped local shadows a typed var of the same name), and expressions other than identifier chains are untyped.
- **Upper bound.** Untyped receivers, `new` of an unknown computed path and `call()()` of an unknown target reach every candidate. Sleeps are path-insensitive: a call guarded by `if(!force)` counts even where tg always passes `force = TRUE` (for example `/mob/living/forceMove` -> `unbuckle_mob(force = TRUE)` -> `/obj/structure/punji_sticks/unbuckle_mob`, which calls `do_after` only when `!force`). Such links are where `Waiting.NoSleep` sites with `may_suspend = yes` come from.
- **Not modelled.** Procs that the engine calls implicitly (operator overloads, `Topic`, `Click`/`MouseDrop` and other client input, `Login`/`Logout` on connection, `Read`/`Write` on savefiles, `Del` on hard deletion) are colour sources only through their own bodies; their engine-side call sites belong to the shim. Object-creating var defaults (`var/datum/x = new /datum/y`) run in the pre-`New` hook and are not counted as `new` calls; type-level var initialisers are not scanned at all (in the snapshot about 110 of them call a proc, mostly `typecacheof` and `mutable_appearance`, and none reaches a proc that can suspend). A `call()()` whose proc is a text name computed at run time (`call(obj, "[name]")`) counts as a sleep (upper bound); calls by name through `vars`, `hascall` or `text2path` are not resolved. A call inside the arguments of a multi-line macro call is reported at the macro's first line.
- **Production build only.** `UNIT_TESTS` and `TESTING` code is not scanned; re-run with a define when tests are translated.
- **Comparison with the review scan** (wave-0 W1/W9): that scan put the lower bound at 2,958 names because it ignored every receiver-qualified call on a name with several roots; this scan types receivers, so its lower and upper bounds are close. That scan's upper bound (7,433 names, 35,796 definitions, slot-level propagation) matches this scan's comparison row without dispatch narrowing (about 34,900 definitions that may sleep); narrowing removes about a third of it.

## Summary (generated)

<!-- generated:summary:begin -->
| Quantity | Value |
|---|---|
| tg snapshot | `1d99bd7f` |
| Files parsed (included by tgstation.dme, recursively) | 7210 |
| Lines parsed | 1226607 |
| Proc definitions (procs and verbs, without builtins) | 65633 |
| Slots with tg definitions (root path + name) | 25345 |
| Coloured slots, upper bound (the committed list) | 8028 (7200 distinct names) |
| Coloured slots, lower bound (typed calls only) | 7650 |
| Coloured slots whose callers can be suspended (`suspends`) | 7929 |
| Comparison: coloured slots / definitions that may sleep if every call reached every definition of the callee slot (no dispatch narrowing) | 12788 / 34834 |
| Definitions in coloured slots | 34336 (52.3%) |
| Definitions in slots that suspend (`DmTask` signature, waiting-model.md 4.2) | 33417 (50.9%) |
| Definitions that may sleep, upper / lower bound | 23308 / 22421 |
| Sync-mandated definitions (SIGNAL_HANDLER, SHOULD_NOT_SLEEP, own or inherited) | 12058 |
| Definitions with effective `waitfor = FALSE` (own or inherited) | 954 |
| Overriding `New` definitions that may sleep (`sleeping-new.tsv`) | 299 |
| NoSleep sites (`nosleep-sites.tsv`) | 10331 |
| &nbsp;&nbsp;context `SIGNAL_HANDLER`: may suspend / always completed | 2429 / 403 |
| &nbsp;&nbsp;context `SHOULD_NOT_SLEEP`: may suspend / always completed | 4082 / 1134 |
| &nbsp;&nbsp;context `sync`: may suspend / always completed | 0 / 2227 |
| &nbsp;&nbsp;context `RegisterSignal`: may suspend / always completed | 56 / 0 |
| Direct sleep-primitive sites (seeds) | 673 |
| `call()()` sites: unknown target (count as a sleep) / resolved by convention | 36 / 25 |
| Calls: unqualified on src / global / typed receiver | 52547 / 46863 / 66005 |
| Calls: untyped receiver / typed receiver without the proc / below the receiver type / unresolved name | 625 / 0 / 0 / 0 |
| `new`: static type / bounded computed type / unknown computed type | 11145 / 1380 / 446 |
| `..()` calls / builtin-proc calls / step() calls | 23511 / 70099 / 130 |
| Unparsed type-level items / unclosed brace blocks / implicit roots (override with no declaration found) | 0 / 0 / 0 |
| Active `#error` directives / missing includes | 0 / 0 |
<!-- generated:summary:end -->

## Wait primitives found (generated)

<!-- generated:seeds:begin -->
Builtin primitives (seeds) and their call sites: `sleep` 400, `input` 239, `alert` 22, `winget` 6, `winexists` 4, `world.Export` 4, `client.MeasureText` 3, `shell` 2, `client.SoundQuery` 1, `world.IsSubscribed` 1.
`call()()` with an unknown target counts as a sleep: 36 sites in 28 defs (e.g. `/datum/callback/Invoke`).

tg procs that sleep directly (their own body calls a primitive, outside spawn/ASYNC), by call sites:

| Proc | Primitive | Slot coloured | Suspends callers | Call sites |
|---|---|---|---|---|
| `/proc/tgui_alert` (code/modules/tgui_input/alert.dm:13) | `alert` | yes | yes | 468 |
| `/proc/tgui_input_list` (code/modules/tgui_input/list.dm:13) | `input` | yes | yes | 303 |
| `/proc/tgui_input_text` (code/modules/tgui_input/text.dm:18) | `input` | yes | yes | 220 |
| `/datum/controller/subsystem/job/Recover` (code/controllers/subsystem/job.dm:720) | `sleep` | yes | no | 143 |
| `/proc/tgui_input_number` (code/modules/tgui_input/number.dm:18) | `input` | yes | yes | 112 |
| `/obj/structure/necropolis_gate/legion_gate/attack_hand` (code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:162) | `sleep` | yes | yes | 104 |
| `/proc/stoplag` (code/__HELPERS/stoplag.dm:7) | `sleep` | yes | yes | 97 |
| `/obj/item/coin/attack_self` (code/modules/mining/ores_coins.dm:612) | `sleep` | yes | yes | 61 |
| `/obj/item/coin/gold/debug/attack_self` (code/modules/mining/ores_coins.dm:710) | `sleep` | yes | yes | 61 |
| `/obj/item/debug/human_spawner/attack_self` (code/game/objects/items/debug_items.dm:28) | `input` | yes | yes | 61 |
| `/obj/item/debug/omnitool/item_spawner/attack_self` (code/game/objects/items/debug_items.dm:73) | `sleep` | yes | yes | 61 |
| `/obj/item/toy/nuke/attack_self` (code/game/objects/items/toys.dm:1000) | `sleep` | yes | yes | 61 |
| `/obj/item/toy/toy_xeno/attack_self` (code/game/objects/items/toys.dm:1185) | `sleep` | yes | yes | 61 |
| `/obj/item/warp_cube/attack_self` (code/modules/mining/lavaland/mining_loot/equipment.dm:89) | `sleep` | yes | yes | 61 |
| `/datum/browser/modal/open` (code/datums/browser.dm:245) | `winget` | yes | no | 49 |
| `/obj/machinery/door/airlock/open` (code/game/machinery/doors/airlock.dm:1327) | `sleep` | yes | yes | 45 |
| `/obj/machinery/door/airlock/tram/open` (code/modules/transport/tram/tram_doors.dm:31) | `sleep` | yes | yes | 45 |
| `/obj/machinery/door/open` (code/game/machinery/doors/door.dm:584) | `sleep` | yes | yes | 45 |
| `/obj/machinery/door/window/open` (code/game/machinery/doors/windowdoor.dm:238) | `sleep` | yes | yes | 45 |
| `/proc/tgui_color_picker` (code/modules/tgui_input/color.dm:11) | `input` | yes | yes | 37 |
| `/mob/spin` (code/modules/mob/mob.dm:748) | `sleep` | yes | no | 26 |
| `/obj/structure/filingcabinet/item_interaction` (code/modules/paperwork/filingcabinet.dm:46) | `sleep` | yes | yes | 26 |
| `/proc/pick_closest_path` (code/__HELPERS/atoms.dm:275) | `input` | yes | yes | 25 |
| `/obj/machinery/door/airlock/close` (code/game/machinery/doors/airlock.dm:1410) | `sleep` | yes | yes | 23 |
| `/obj/machinery/door/close` (code/game/machinery/doors/door.dm:619) | `sleep` | yes | yes | 23 |
| `/obj/machinery/door/window/close` (code/game/machinery/doors/windowdoor.dm:287) | `sleep` | yes | yes | 23 |
| `/obj/structure/speaking_tile/SpeakPeace` (code/modules/awaymissions/super_secret_room.dm:111) | `sleep` | yes | yes | 22 |
| `/client/debug_variables` (code/modules/admin/view_variables/view_variables.dm:9) | `sleep` | yes | yes | 20 |
| `/datum/action/cooldown/mob_cooldown/projectile_attack/dir_shots/alternating/colossus/Activate` (code/datums/actions/mobs/projectileattack.dm:300) | `sleep` | yes | yes | 16 |
| `/datum/action/cooldown/mob_cooldown/projectile_attack/random_aoe/colossus/Activate` (code/datums/actions/mobs/projectileattack.dm:200) | `sleep` | yes | yes | 16 |

376 defs sleep directly; 22932 more are coloured through calls (the full list is the `colored = yes` rows of `colored-slots.tsv`).
<!-- generated:seeds:end -->

## M0 base slots (generated)

<!-- generated:m0:begin -->
| M0 proc | Slot (root path / name) | Coloured | Suspends callers | waitfor FALSE | Sync-mandated | Lower bound | Defs | Call sites | Witness chain |
|---|---|---|---|---|---|---|---|---|---|
| datum New | `/datum/New` | yes | yes | no | no | yes | 1230 | 12442 | `/datum/select_equipment/New (code/modules/admin/verbs/selectequipment.dm:41) -> /proc/tgui_alert (code/modules/tgui_input/alert.dm:33) -> alert` |
| datum Destroy (also atom Destroy) | `/datum/Destroy` | no | no | no | yes | no | 1732 | 2 |  |
| datum Initialize | `/datum/Initialize`: no such slot (no root declaration there) | | | | | | | | |
| component Initialize | `/datum/component/Initialize` | yes | yes | no | no | yes | 363 | 1 | `/datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| atom Initialize | `/atom/Initialize` | no | no | no | yes | no | 3870 | 1 |  |
| atom LateInitialize | `/atom/LateInitialize` | yes | no | yes | no | yes | 71 | 3 | `/obj/effect/mapping_helpers/atom_injector/LateInitialize (code/modules/mapping/mapping_helpers.dm:733) -> /obj/effect/mapping_helpers/atom_injector/custom_icon/check_validity (code/modules/mapping/mapping_helpers.dm:899) -> /proc/stoplag (code/__HELPERS/stoplag.dm:9) -> sleep` |
| atom Entered | `/atom/Entered` | yes | yes | some | no | yes | 61 | 10 | `/obj/effect/wisp_mobile/Entered (code/modules/antagonists/voidwalker/voidwalker_kidnap.dm:57) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| atom Exited | `/atom/Exited` | yes | yes | no | no | yes | 198 | 11 | `/turf/open/space/transit/Exited (code/game/turfs/open/space/transit.dm:53) -> /proc/dump_in_space (code/game/turfs/open/space/transit.dm:81) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| atom Enter | `/atom/Enter` | yes | yes | no | no | yes | 2 | 15 | `/turf/Enter (code/game/turfs/turf.dm:452) -> /obj/vehicle/sealed/Bump (code/modules/vehicles/sealed.dm:44) -> /obj/machinery/door/airlock/tram/try_safety_unlock (code/modules/transport/tram/tram_doors.dm:230) -> /obj/machinery/door/airlock/tram/open (code/modules/transport/tram/tram_doors.dm:51) -> sleep` |
| atom Exit | `/atom/Exit` | yes | yes | no | no | yes | 2 | 4 | `/obj/structure/closet/Exit (code/game/objects/structures/crates_lockers/closets.dm:1080) -> /obj/structure/closet/open (code/game/objects/structures/crates_lockers/closets.dm:550) -> /obj/structure/closet/dump_contents (code/game/objects/structures/crates_lockers/closets.dm:515) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| movable Bump | `/atom/movable/Bump` | yes | yes | no | no | yes | 29 | 20 | `/obj/vehicle/sealed/Bump (code/modules/vehicles/sealed.dm:44) -> /obj/machinery/door/airlock/tram/try_safety_unlock (code/modules/transport/tram/tram_doors.dm:230) -> /obj/machinery/door/airlock/tram/open (code/modules/transport/tram/tram_doors.dm:51) -> sleep` |
| atom Bumped | `/atom/Bumped` | yes | no | yes | no | yes | 44 | 6 | `/obj/machinery/door/Bumped (code/game/machinery/doors/door.dm:282) -> /obj/machinery/door/airlock/open (code/game/machinery/doors/airlock.dm:1368) -> sleep` |
| atom update_icon | `/atom/update_icon` | yes | yes | no | no | yes | 50 | 161 | `/obj/machinery/atmospherics/update_icon (code/modules/atmospherics/machinery/atmosmachinery.dm:245) -> /obj/machinery/atmospherics/update_cap_visuals (code/modules/atmospherics/machinery/atmosmachinery.dm:662) -> /atom/movable/moveToNullspace (code/game/atoms_movable.dm:1167) -> /obj/golfcart_rear/doMove (code/modules/vehicles/cars/golfcart/golfcart_rear.dm:224) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| atom update_appearance | `/atom/update_appearance` | no | no | no | yes | no | 50 | 2804 |  |
| atom setDir | `/atom/setDir` | yes | yes | no | no | yes | 31 | 289 | `/obj/machinery/conveyor/setDir (code/modules/recycling/conveyor.dm:154) -> /obj/machinery/conveyor/update_move_direction (code/modules/recycling/conveyor.dm:231) -> /obj/machinery/conveyor/update (code/modules/recycling/conveyor.dm:258) -> /obj/machinery/conveyor/start_conveying (code/modules/recycling/conveyor.dm:288) -> /datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| atom set_light | `/atom/set_light` | yes | yes | no | no | yes | 1 | 214 | `/atom/set_light (code/modules/lighting/lighting_atom.dm:35) -> /obj/item/clothing/head/utility/hardhat/pumpkinhead/set_light_on (code/modules/clothing/head/hardhat.dm:267) -> /datum/_AddElement (code/datums/elements/_element.dm:68) -> /datum/element/fried_item/Attach (code/datums/elements/food/fried_item.dm:46) -> /datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| movable Move | `/atom/movable/Move` | yes | yes | no | no | yes | 58 | 221 | `/atom/movable/Move (code/game/atoms_movable.dm:723) -> /turf/Enter (code/game/turfs/turf.dm:452) -> /obj/vehicle/sealed/Bump (code/modules/vehicles/sealed.dm:44) -> /obj/machinery/door/airlock/tram/try_safety_unlock (code/modules/transport/tram/tram_doors.dm:230) -> /obj/machinery/door/airlock/tram/open (code/modules/transport/tram/tram_doors.dm:51) -> sleep` |
| movable Moved | `/atom/movable/Moved` | yes | yes | no | no | yes | 79 | 7 | `/atom/movable/Moved (code/game/atoms_movable.dm:914) -> /obj/machinery/computer/bank_machine/on_changed_z_level (code/game/machinery/bank_machine.dm:130) -> /atom/movable/say (code/game/say.dm:57) -> /mob/try_speak (code/modules/mob/mob_say.dm:68) -> /proc/tgui_alert (code/modules/tgui_input/alert.dm:33) -> alert` |
| movable forceMove | `/atom/movable/forceMove` | yes | yes | no | no | yes | 11 | 1548 | `/mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| movable doMove | `/atom/movable/doMove` | yes | yes | no | no | yes | 3 | 3 | `/obj/golfcart_rear/doMove (code/modules/vehicles/cars/golfcart/golfcart_rear.dm:224) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| movable moveToNullspace | `/atom/movable/moveToNullspace` | yes | yes | no | no | yes | 1 | 62 | `/atom/movable/moveToNullspace (code/game/atoms_movable.dm:1167) -> /obj/golfcart_rear/doMove (code/modules/vehicles/cars/golfcart/golfcart_rear.dm:224) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| process | `/datum/process` | yes | no | yes | no | yes | 461 | 22 | `/obj/machinery/computer/pod/process (code/game/machinery/computer/pod.dm:30) -> /obj/machinery/computer/pod/alarm (code/game/machinery/computer/pod.dm:47) -> sleep` |
| Life | `/mob/living/Life` | no | no | yes | yes | no | 35 | 1 |  |
| subsystem fire | `/datum/controller/subsystem/fire` | yes | yes | some | no | yes | 105 | 6 | `/datum/controller/subsystem/dynamic/fire (code/controllers/subsystem/dynamic/dynamic.dm:67) -> /datum/controller/subsystem/dynamic/try_spawn_midround (code/controllers/subsystem/dynamic/dynamic.dm:368) -> /proc/stoplag (code/__HELPERS/stoplag.dm:9) -> sleep` |
| subsystem ignite | `/datum/controller/subsystem/ignite` | yes | no | yes | no | yes | 1 | 1 | `/datum/controller/subsystem/ignite (code/controllers/subsystem.dm:148) -> /datum/controller/subsystem/dynamic/fire (code/controllers/subsystem/dynamic/dynamic.dm:67) -> /datum/controller/subsystem/dynamic/try_spawn_midround (code/controllers/subsystem/dynamic/dynamic.dm:368) -> /proc/stoplag (code/__HELPERS/stoplag.dm:9) -> sleep` |
| controller Initialize | `/datum/controller/Initialize` | yes | yes | some | no | yes | 75 | 8 | `/datum/controller/subsystem/air/Initialize (code/controllers/subsystem/air.dm:99) -> /datum/controller/subsystem/air/setup_allturfs (code/controllers/subsystem/air.dm:583) -> sleep` |
| callback Invoke | `/datum/callback/Invoke` | yes | yes | no | no | no | 2 | 223 | `/datum/callback/Invoke (code/datums/callback.dm:118) -> call()() call(delegate)` |
| callback InvokeAsync | `/datum/callback/InvokeAsync` | yes | no | yes | no | no | 2 | 33 | `/datum/callback/InvokeAsync (code/datums/callback.dm:155) -> call()() call(delegate)` |
| do_after | `/proc/do_after` | yes | yes | no | no | yes | 1 | 793 | `/proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| stoplag | `/proc/stoplag` | yes | yes | no | no | yes | 1 | 97 | `/proc/stoplag (code/__HELPERS/stoplag.dm:9) -> sleep` |
| client New | `/client/New` | yes | yes | no | no | yes | 1 | 4 | `/client/New (code/modules/client/client_procs.dm:579) -> winexists` |

Notable definitions (the def itself, not its slot):

| Definition | Slot | May sleep | waitfor FALSE | Sync-mandated | Witness chain |
|---|---|---|---|---|---|
| `/datum/controller/master/Initialize` (code/controllers/master.dm:323) | `/datum/controller/Initialize` | yes | yes | no | `/datum/controller/master/Initialize (code/controllers/master.dm:327) -> sleep` |
| `/datum/controller/failsafe/Initialize` (code/controllers/failsafe.dm:37) | `/datum/controller/Initialize` | yes | yes | no | `/datum/controller/failsafe/Initialize (code/controllers/failsafe.dm:54) -> sleep` |
| `/datum/controller/subsystem/Initialize` (code/controllers/subsystem.dm:289) | `/datum/controller/Initialize` | no | no | no |  |
| `/datum/controller/master/Loop` (code/controllers/master.dm:588) | `/datum/controller/master/Loop` | yes | no | no | `/datum/controller/master/Loop (code/controllers/master.dm:672) -> sleep` |
| `/datum/controller/master/RunQueue` (code/controllers/master.dm:831) | `/datum/controller/master/RunQueue` | no | no | no |  |
| `/datum/controller/subsystem/fire` (code/controllers/subsystem.dm:163) | `/datum/controller/subsystem/fire` | no | no | no |  |
| `/datum/controller/subsystem/processing/fire` (code/controllers/subsystem/processing/processing.dm:17) | `/datum/controller/subsystem/fire` | no | no | no |  |
| `/datum/controller/subsystem/timer/fire` (code/controllers/subsystem/timer.dm:85) | `/datum/controller/subsystem/fire` | no | no | no |  |
| `/datum/controller/subsystem/machines/fire` (code/controllers/subsystem/machines.dm:92) | `/datum/controller/subsystem/fire` | yes | no | no | `/datum/controller/subsystem/machines/fire (code/controllers/subsystem/machines.dm:126) -> /obj/machinery/power/apc/early_process (code/modules/power/apc/apc_main.dm:589) -> /obj/item/stock_parts/power_store/use (code/modules/power/power_store.dm:160) -> /obj/item/stock_parts/power_store/try_explode (code/modules/power/power_store.dm:209) -> /datum/reagents/spark_act (code/modules/reagents/chemistry/holder/holder.dm:795) -> /datum/reagent/phlogiston/on_spark_act (code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:291) -> /obj/machinery/vending/wardrobe/take_damage (code/modules/vending/wardrobes.dm:38) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/datum/controller/subsystem/mobs/fire` (code/controllers/subsystem/mobs.dm:30) | `/datum/controller/subsystem/fire` | no | no | no |  |
| `/atom/movable/Move` (code/game/atoms_movable.dm:763) | `/atom/movable/Move` | yes | no | no | `/atom/movable/Move (code/game/atoms_movable.dm:777) -> ..() -> /atom/movable/Move (code/game/atoms_movable.dm:723) -> /turf/Enter (code/game/turfs/turf.dm:452) -> /obj/vehicle/sealed/Bump (code/modules/vehicles/sealed.dm:44) -> /obj/machinery/door/airlock/tram/try_safety_unlock (code/modules/transport/tram/tram_doors.dm:230) -> /obj/machinery/door/airlock/tram/open (code/modules/transport/tram/tram_doors.dm:51) -> sleep` |
| `/atom/movable/Moved` (code/game/atoms_movable.dm:896) | `/atom/movable/Moved` | yes | no | no | `/atom/movable/Moved (code/game/atoms_movable.dm:914) -> /obj/machinery/computer/bank_machine/on_changed_z_level (code/game/machinery/bank_machine.dm:130) -> /atom/movable/say (code/game/say.dm:57) -> /mob/try_speak (code/modules/mob/mob_say.dm:68) -> /proc/tgui_alert (code/modules/tgui_input/alert.dm:33) -> alert` |
| `/atom/movable/forceMove` (code/game/atoms_movable.dm:1159) | `/atom/movable/forceMove` | yes | no | no | `/atom/movable/forceMove (code/game/atoms_movable.dm:1162) -> /obj/golfcart_rear/doMove (code/modules/vehicles/cars/golfcart/golfcart_rear.dm:224) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/atom/movable/doMove` (code/game/atoms_movable.dm:1169) | `/atom/movable/doMove` | yes | no | no | `/atom/movable/doMove (code/game/atoms_movable.dm:1171) -> /atom/movable/Moved (code/game/atoms_movable.dm:914) -> /obj/machinery/computer/bank_machine/on_changed_z_level (code/game/machinery/bank_machine.dm:130) -> /atom/movable/say (code/game/say.dm:57) -> /mob/try_speak (code/modules/mob/mob_say.dm:68) -> /proc/tgui_alert (code/modules/tgui_input/alert.dm:33) -> alert` |
| `/atom/Entered` (code/game/atom/_atom.dm:620) | `/atom/Entered` | no | no | no |  |
| `/atom/Exited` (code/game/atom/_atom.dm:643) | `/atom/Exited` | no | no | no |  |
| `/turf/Enter` (code/game/turfs/turf.dm:434) | `/atom/Enter` | yes | no | no | `/turf/Enter (code/game/turfs/turf.dm:452) -> /obj/vehicle/sealed/Bump (code/modules/vehicles/sealed.dm:44) -> /obj/machinery/door/airlock/tram/try_safety_unlock (code/modules/transport/tram/tram_doors.dm:230) -> /obj/machinery/door/airlock/tram/open (code/modules/transport/tram/tram_doors.dm:51) -> sleep` |
| `/area/Entered` (code/game/area/areas.dm:581) | `/atom/Entered` | no | yes | no |  |
| `/atom/update_icon` (code/game/atom/atom_appearance.dm:40) | `/atom/update_icon` | yes | no | no | `/atom/update_icon (code/game/atom/atom_appearance.dm:46) -> /obj/structure/ai_core/update_icon_state (code/game/machinery/ai_core/_core.dm:54) -> /atom/set_light (code/modules/lighting/lighting_atom.dm:35) -> /obj/item/clothing/head/utility/hardhat/pumpkinhead/set_light_on (code/modules/clothing/head/hardhat.dm:267) -> /datum/_AddElement (code/datums/elements/_element.dm:68) -> /datum/element/fried_item/Attach (code/datums/elements/food/fried_item.dm:46) -> /datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/atom/setDir` (code/game/atom/_atom.dm:579) | `/atom/setDir` | no | no | no |  |
| `/atom/set_light` (code/modules/lighting/lighting_atom.dm:3) | `/atom/set_light` | yes | no | no | `/atom/set_light (code/modules/lighting/lighting_atom.dm:35) -> /obj/item/clothing/head/utility/hardhat/pumpkinhead/set_light_on (code/modules/clothing/head/hardhat.dm:267) -> /datum/_AddElement (code/datums/elements/_element.dm:68) -> /datum/element/fried_item/Attach (code/datums/elements/food/fried_item.dm:46) -> /datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/datum/New` (builtin) | `/datum/New` | no | no | no |  |
| `/atom/New` (code/game/atom/atoms_initializing_EXPENSIVE.dm:65) | `/datum/New` | no | no | no |  |
| `/datum/Destroy` (code/datums/datum.dm:125) | `/datum/Destroy` | no | no | yes |  |
| `/atom/Destroy` (code/game/atom/_atom.dm:163) | `/datum/Destroy` | no | no | yes |  |
| `/proc/qdel` (code/controllers/subsystem/garbage.dm:346) | `/proc/qdel` | no | no | no |  |
| `/datum/_SendSignal` (code/datums/signals.dm:113) | `/datum/_SendSignal` | no | no | no |  |
<!-- generated:m0:end -->

## M0 base files (generated)

<!-- generated:m0files:begin -->
| tg file | Defs | In slots that suspend (DmTask) | May sleep | Sync-mandated | NoSleep sites: may suspend / completed |
|---|---|---|---|---|---|
| `code/datums/datum.dm` | 28 | 4 | 2 | 2 | 0 / 0 |
| `code/datums/signals.dm` | 4 | 0 | 0 | 0 | 0 / 0 |
| `code/datums/callback.dm` | 7 | 4 | 4 | 1 | 0 / 0 |
| `code/datums/timed_actions.dm` | 16 | 3 | 2 | 10 | 2 / 0 |
| `code/datums/components/_component.dm` | 25 | 16 | 8 | 1 | 1 / 0 |
| `code/datums/elements/_element.dm` | 8 | 3 | 2 | 4 | 0 / 0 |
| `code/game/atom/_atom.dm` | 65 | 29 | 11 | 2 | 0 / 0 |
| `code/game/atom/atoms_initializing_EXPENSIVE.dm` | 4 | 1 | 0 | 1 | 5 / 0 |
| `code/game/atom/atom_appearance.dm` | 6 | 3 | 1 | 1 | 1 / 0 |
| `code/game/atom/atom_color.dm` | 5 | 3 | 2 | 0 | 0 / 0 |
| `code/game/atoms_movable.dm` | 113 | 46 | 33 | 3 | 8 / 8 |
| `code/game/area/areas.dm` | 41 | 9 | 7 | 2 | 0 / 0 |
| `code/game/turfs/turf.dm` | 76 | 31 | 26 | 6 | 5 / 4 |
| `code/game/turfs/open/_open.dm` | 50 | 24 | 18 | 3 | 2 / 0 |
| `code/game/objects/objs.dm` | 36 | 21 | 16 | 2 | 0 / 0 |
| `code/modules/mob/mob.dm` | 118 | 36 | 27 | 13 | 5 / 6 |
| `code/controllers/controller.dm` | 6 | 3 | 0 | 0 | 0 / 0 |
| `code/controllers/master.dm` | 28 | 14 | 13 | 2 | 0 / 2 |
| `code/controllers/failsafe.dm` | 9 | 6 | 5 | 1 | 0 / 0 |
| `code/controllers/subsystem.dm` | 16 | 4 | 1 | 1 | 0 / 0 |
| `code/controllers/subsystem/timer.dm` | 18 | 3 | 0 | 1 | 0 / 1 |
| `code/controllers/subsystem/processing/processing.dm` | 4 | 2 | 0 | 0 | 0 / 0 |
| `code/controllers/subsystem/garbage.dm` | 12 | 4 | 0 | 0 | 0 / 1 |
| `code/__HELPERS/stoplag.dm` | 1 | 1 | 1 | 0 | 0 / 0 |
<!-- generated:m0files:end -->

## Most frequent NoSleep conflicts (generated)

Callees of the `nosleep-sites.tsv` rows with `may_suspend = yes` in a `SIGNAL_HANDLER` or `SHOULD_NOT_SLEEP` caller, by number of sites (a site with several candidate slots counts for each).

<!-- generated:conflicts:begin -->
| Callee slot | Conflict sites | Witness chain |
|---|---|---|
| `/datum/_AddElement` | 1495 | `/datum/_AddElement (code/datums/elements/_element.dm:68) -> /datum/element/fried_item/Attach (code/datums/elements/food/fried_item.dm:46) -> /datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/datum/_AddComponent` | 887 | `/datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/datum/New` | 206 | `/datum/select_equipment/New (code/modules/admin/verbs/selectequipment.dm:41) -> /proc/tgui_alert (code/modules/tgui_input/alert.dm:33) -> alert` |
| `/atom/movable/forceMove` | 162 | `/mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/datum/reagents/add_reagent` | 143 | `/datum/reagents/add_reagent (code/modules/reagents/chemistry/holder/holder.dm:175) -> /datum/reagent/eigenstate/on_mob_add (code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:76) -> /proc/do_teleport (code/datums/helper_datums/teleport.dm:108) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/datum/action/Grant` | 98 | `/datum/action/Grant (code/datums/actions/action.dm:93) -> /datum/action/cooldown/spell/jaunt/Remove (code/modules/spells/spell_types/jaunt/_jaunt.dm:120) -> /datum/action/cooldown/spell/jaunt/mirror_walk/exit_jaunt (code/modules/antagonists/heretic/magic/mirror_walk.dm:100) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/atom/set_light` | 91 | `/atom/set_light (code/modules/lighting/lighting_atom.dm:35) -> /obj/item/clothing/head/utility/hardhat/pumpkinhead/set_light_on (code/modules/clothing/head/hardhat.dm:267) -> /datum/_AddElement (code/datums/elements/_element.dm:68) -> /datum/element/fried_item/Attach (code/datums/elements/food/fried_item.dm:46) -> /datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/datum/callback/Invoke` | 74 | `/datum/callback/Invoke (code/datums/callback.dm:118) -> call()() call(delegate)` |
| `/mob/living/add_mood_event` | 62 | `/mob/living/add_mood_event (code/modules/mob/living/living.dm:2876) -> /datum/mood/add_conditional_mood_event (code/datums/mood.dm:239) -> /datum/mood/add_mood_event_instance (code/datums/mood.dm:190) -> /datum/mood_event/on_add (code/datums/mood_events/_mood_event.dm:99) -> /datum/mood_event/drunk/add_effects (code/datums/mood_events/drink_events.dm:12) -> /mob/living/carbon/human/give_emote_overlay (code/datums/bodypart_overlays/emote_bodypart_overlay.dm:45) -> /obj/item/bodypart/add_bodypart_overlay (code/modules/surgery/bodyparts/_bodyparts.dm:1542) -> /mob/living/carbon/update_body_parts (code/modules/mob/living/carbon/carbon_update_icons.dm:417) -> /mob/living/carbon/human/update_eyes (code/modules/mob/living/carbon/human/human_update_icons.dm:714) -> /obj/item/organ/eyes/refresh (code/modules/surgery/organs/internal/eyes/_eyes.dm:117) -> /mob/living/carbon/update_tint (code/modules/mob/living/carbon/carbon.dm:465) -> /mob/living/apply_status_effect (code/datums/status_effects/_status_effect_helpers.dm:43) -> /datum/status_effect/New (code/datums/status_effects/_status_effect.dm:39) -> /datum/status_effect/on_creation (code/datums/status_effects/_status_effect.dm:47) -> /datum/status_effect/terrified/on_apply (code/datums/status_effects/debuffs/terrified.dm:14) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/mob/living/apply_damage` | 62 | `/mob/living/apply_damage (code/modules/mob/living/damage_procs.dm:69) -> /mob/living/adjust_brute_loss (code/modules/mob/living/damage_procs.dm:281) -> /mob/living/on_damage_loss (code/modules/mob/living/damage_procs.dm:477) -> /mob/living/simple_animal/hostile/megafauna/legion/on_damage_loss_changed (code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:146) -> /obj/structure/necropolis_gate/legion_gate/toggle_the_gate (code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:175) -> ..() -> /obj/structure/necropolis_gate/toggle_the_gate (code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:111) -> sleep` |
| `/atom/Adjacent` | 43 | `/turf/Adjacent (code/_onclick/adjacent.dm:40) -> /turf/ClickCross (code/_onclick/adjacent.dm:99) -> /atom/CanPass (code/game/atom/_atom.dm:232) -> /obj/machinery/prisongate/CanAllowThrough (code/game/machinery/prisongate.dm:57) -> /atom/movable/say (code/game/say.dm:57) -> /mob/try_speak (code/modules/mob/mob_say.dm:68) -> /proc/tgui_alert (code/modules/tgui_input/alert.dm:33) -> alert` |
| `/mob/grant_actions_by_list` | 42 | `/mob/grant_actions_by_list (code/modules/mob/mob_helpers.dm:584) -> /datum/action/Grant (code/datums/actions/action.dm:93) -> /datum/action/cooldown/spell/jaunt/Remove (code/modules/spells/spell_types/jaunt/_jaunt.dm:120) -> /datum/action/cooldown/spell/jaunt/mirror_walk/exit_jaunt (code/modules/antagonists/heretic/magic/mirror_walk.dm:100) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/mob/living/apply_status_effect` | 38 | `/mob/living/apply_status_effect (code/datums/status_effects/_status_effect_helpers.dm:43) -> /datum/status_effect/New (code/datums/status_effects/_status_effect.dm:39) -> /datum/status_effect/on_creation (code/datums/status_effects/_status_effect.dm:47) -> /datum/status_effect/terrified/on_apply (code/datums/status_effects/debuffs/terrified.dm:14) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/obj/find_and_mount_on_atom` | 38 | `/obj/find_and_mount_on_atom (code/datums/components/atom_mounted.dm:196) -> /datum/_AddComponent (code/datums/components/_component.dm:348) -> /datum/component/New (code/datums/components/_component.dm:47) -> /datum/component/gunpoint/Initialize (code/datums/components/gunpoint.dm:61) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/atom/create_storage` | 37 | `/atom/create_storage (code/game/atom/atom_storage.dm:21) -> /datum/storage/New (code/datums/storage/storage.dm:150) -> /datum/storage/set_real_location (code/datums/storage/storage.dm:276) -> /datum/storage/remove_all (code/datums/storage/storage.dm:666) -> /datum/storage/refresh_views (code/datums/storage/storage.dm:1050) -> /datum/storage/show_contents (code/datums/storage/storage.dm:1082) -> /obj/item/mob_holder/on_found (code/modules/mob/living/inhand_holder.dm:113) -> /obj/item/mob_holder/release (code/modules/mob/living/inhand_holder.dm:89) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/atom/add_atom_colour` | 31 | `/atom/add_atom_colour (code/game/atom/atom_color.dm:36) -> /obj/machinery/light/update_atom_colour (code/modules/power/lighting/light.dm:315) -> /obj/machinery/light/update (code/modules/power/lighting/light.dm:246) -> /datum/reagents/spark_act (code/modules/reagents/chemistry/holder/holder.dm:795) -> /datum/reagent/phlogiston/on_spark_act (code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:291) -> /obj/machinery/vending/wardrobe/take_damage (code/modules/vending/wardrobes.dm:38) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/atom/movable/say` | 30 | `/mob/dead/observer/say (code/modules/mob/dead/observer/observer_say.dm:35) -> /proc/tgui_alert (code/modules/tgui_input/alert.dm:33) -> alert` |
| `/mob/living/Knockdown` | 29 | `/mob/living/Knockdown (code/modules/mob/living/status_procs.dm:103) -> /mob/living/apply_status_effect (code/datums/status_effects/_status_effect_helpers.dm:43) -> /datum/status_effect/New (code/datums/status_effects/_status_effect.dm:39) -> /datum/status_effect/on_creation (code/datums/status_effects/_status_effect.dm:47) -> /datum/status_effect/terrified/on_apply (code/datums/status_effects/debuffs/terrified.dm:14) -> /mob/emote (code/modules/mob/emote.dm:39) -> /datum/emote/imaginary_friend/custom/run_emote (code/datums/brain_damage/imaginary_friend.dm:375) -> input` |
| `/atom/update_icon` | 26 | `/obj/machinery/atmospherics/update_icon (code/modules/atmospherics/machinery/atmosmachinery.dm:245) -> /obj/machinery/atmospherics/update_cap_visuals (code/modules/atmospherics/machinery/atmosmachinery.dm:662) -> /atom/movable/moveToNullspace (code/game/atoms_movable.dm:1167) -> /obj/golfcart_rear/doMove (code/modules/vehicles/cars/golfcart/golfcart_rear.dm:224) -> /mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob (code/modules/hydroponics/grown/towercap.dm:191) -> /proc/do_after (code/datums/timed_actions.dm:243) -> /datum/timed_action/await (code/datums/timed_actions.dm:108) -> sleep` |
| `/mob/living/adjust_brute_loss` | 24 | `/mob/living/adjust_brute_loss (code/modules/mob/living/damage_procs.dm:281) -> /mob/living/on_damage_loss (code/modules/mob/living/damage_procs.dm:477) -> /mob/living/simple_animal/hostile/megafauna/legion/on_damage_loss_changed (code/modules/mob/living/simple_animal/hostile/megafauna/legion.dm:146) -> /obj/structure/necropolis_gate/legion_gate/toggle_the_gate (code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:175) -> ..() -> /obj/structure/necropolis_gate/toggle_the_gate (code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:111) -> sleep` |
<!-- generated:conflicts:end -->

## What this means for M0

Written from the snapshot's numbers above; re-check after a regeneration.

- **The Move chain is async from the first file.** `Move`, `Moved`, `forceMove`, `doMove`, `moveToNullspace`, `Entered`, `Exited`, `Enter`, `Exit`, `Bump`, `setDir`, `update_icon` and `set_light` are coloured slots that suspend callers, already in the lower bound. No definition of these slots anywhere in the tree calls a sleep primitive directly; they are coloured through calls (the witness chains above, such as `/mob/living/forceMove` -> `unbuckle_mob` -> `do_after`), so a file-by-file translation cannot see it coming, and the shared C# virtual signatures are `DmTask`. Translate these signatures as `DmTask`/`DmTask<T>` from the start, so that M0 files are not re-translated when gameplay files arrive (review finding W1). `docs/ai/specs/move-chain.md` ("chain itself never sleeps") is right about tg's chain definitions and wrong about their C# signatures.
- **The lifecycle stays synchronous.** `/atom/Initialize`, `/datum/Destroy` (with every `Destroy` override), `/atom/update_appearance` and `/mob/living/Life` are sync-mandated and not coloured. Their calls into slots that suspend (`forceMove`, `moveToNullspace`, `set_light`, `update_icon`, `add_atom_colour`, `AddComponent`, `AddElement`, ...) are `Waiting.NoSleep` sites; the per-file table above gives the counts for the M0 files and `nosleep-sites.tsv` gives every site.
- **`/datum/New` is coloured.** Some `New` overrides sleep (`sleeping-new.tsv`: admin and UI datums, controllers, components through `Initialize`). `/atom/New` does not sleep (it reaches `Initialize`, which is sync-mandated). `new` is dynamic in the shim: `Dm.@new` is synchronous and `Dm.new_async` is the awaitable creation path that async code uses for the types whose `New` has `suspends = yes` in `sleeping-new.tsv` (translation-rules.md 10.1, object-model.md section 4).
- **`waitfor = FALSE` slots never suspend callers.** `/datum/process`, `/datum/controller/subsystem/ignite`, `/datum/callback/InvokeAsync`, `/atom/Bumped`, `/atom/LateInitialize` are coloured with `suspends = no`: their signatures stay synchronous and the definitions that may sleep get the callee-side wrapper of waiting-model.md 5.4 (`/mob/living/Life` is `waitfor = FALSE` too, but no definition of it may sleep, so it needs no wrapper). Their callers are not coloured through them: `/datum/controller/master/RunQueue` stays synchronous, and its `queue_node.ignite(...)` (master.dm:902) is a plain call; the processing subsystems read `PROCESS_KILL` from `process()` without waiting.
- **The controller.** `/datum/controller/Initialize` is coloured: subsystem overrides sleep (for example `SSair` through `setup_allturfs`), and the master and failsafe `Initialize` are `waitfor = FALSE` and sleep. `/datum/controller/subsystem/fire` is coloured and suspends (`SSdynamic` waits for admins through `stoplag` in `try_spawn_midround`; `SSlighting`'s `stoplag` follows `if(!TICK_CHECK) continue` and is dead in the port); tg's `ignite` is `waitfor = FALSE`, so the MC carries on when a `fire` sleeps and sees `SS_SLEEPING`. `/datum/controller/master/Loop` sleeps: it is the scheduler coroutine of waiting-model.md 7.2.
- **Callbacks and signals.** `/datum/callback/Invoke` is coloured only by the upper bound (its target is computed); callers await it, or use `NoSleep` in sync-mandated code. `_SendSignal` is not coloured; the `RegisterSignal` rows of `nosleep-sites.tsv` list the registered handlers that live in slots that suspend (the handler is not marked `SIGNAL_HANDLER`, or an override of it sleeps), which `_SendSignal` must call through `NoSleep`.
- **Where the conflicts concentrate.** `AddElement` and `AddComponent` account for most `may_suspend = yes` sites (table above): element `Attach` and component `New`/`Initialize` are virtual over every element and component, and a few of them reach a sleep through path-insensitive links. Whether such procs are declared no-sleep by contract (tg's lint already forbids sleeping in the sync-mandated callers) is a question for the owner and the waiting spec; the scanner reports the sites either way.

## Type order (`TypeOrder.g.cs`, `ProcOrder.g.cs`)

`docs/ai/specs/object-model.md` section 9 fixes the order in which the port enumerates types (`typesof`, `subtypesof`, type caches, subsystem creation): depth-first preorder of the type tree, children in the order of their first declaration in compile order, which is OpenDream's order. `tools/coloring/typeorder.py` reproduces OpenDream's algorithm step by step from its sources (OpenDreamProject/OpenDream, master):

1. **Code tree** (`DMCompiler/DM/DMCodeTree.cs`). Every type declaration, var declaration, var override and proc definition adds its type path to a code tree, one node per path segment, a node's children in order of first mention (`AddType` -> `GetDMObjectNode`). The scanner records the same mentions in parse order (`.dme` include order, then line order; a path's missing prefixes first). OpenDream's own builtin library (`DMStandard`, parsed before the program) is a separate tree; its type declarations are listed in `typeorder.py` (`DMSTANDARD_TYPES`, from `DMStandard/_Standard.dm` and `Types/*.dm`).
2. **Type ids** (`DMCodeTree.DefineEverything`, `DMObjectTree.GetOrCreateDMObject`). Preorder passes over the program's tree, then over the DMStandard tree, repeated while types are waiting. Defining a type gives it the next id, after creating its missing prefix types; a one-segment path is created under `/datum`, except `client`, `datum`, `list`, `alist`, `vector`, `savefile`, `world` and `callee`, which are children of the root. A type with an explicit `parent_type` waits until that parent exists and is then re-parented; the first assignment wins, and DMStandard's (`/atom` -> `/datum`, `/obj` and `/mob` -> `/atom/movable`, `/turf` and `/area` -> `/atom`, `/mutable_appearance` -> `/image`, ...) come before the program's.
3. **Order** (`OpenDreamRuntime/Objects/DreamObjectTree.cs`). A type's children are its inheriting types in id order, and `typesof` (`DreamProcNativeRoot.NativeProc_typesof` -> `GetAllDescendants`) is the depth-first preorder over them.

For types under their path prefix this is exactly "children in the order of first declaration". A type whose parent is not its path prefix (a one-segment type such as `/atom` or `/client` under `/datum`, `/obj` and `/mob` under `/atom/movable`, `/turf` and `/area` under `/atom`) gets its id when the passes reach its own root-level node, after every type of the subtrees mentioned before it, so it sorts after those siblings even when tg declares it earlier (the table below lists every such case). BYOND's own order has not been compared with a BYOND run (object-model.md section 9); a difference would only change the order between such subtrees.

`TypeOrder.g.cs` (`Anomaly.Core.Tg.TypeOrder.Paths`, an `ImmutableArray<string>`, and `TypeOrder.Count`) lists every path, builtin types included, in that order; the shim's type registry finds both generated classes through `[assembly: DmTypeOrder(typeof(TypeOrder), typeof(ProcOrder))]` in `src/Core/Tg/Infrastructure/TypeOrderSource.cs` (hand-written, not generated), and `tests/Anomaly.Core.Tests/Tools/TypeOrderTests.cs` checks them from C#. The list is stored as one raw string literal with one line per type, its depth and the segment it adds to its parent's path (a full path after `=` when the parent is not the prefix), and decoded once (about 46,000 types; 560 KB of source, compiled in well under a second). `ProcOrder.g.cs` serves `typesof(/t/proc)` and `typesof(/t/verb)`: the procs declared on the type itself with the `proc/` keyword (verbs with `verb/`), in definition order, which tg's `/datum/controller/global_vars/Initialize` relies on to run the `InitGlobal*` procs (controllers/globals.dm:51-56; tg warns when their number differs from the number of global vars, so on BYOND the list holds exactly the procs declared there). It is generated only for the types that tg enumerates that way, found in every proc body of the production build; `ProcOrder.ProcsOf` throws for any other type.

### Type order (generated)

<!-- generated:typeorder:begin -->
| Quantity | Value |
|---|---|
| Type paths (`TypeOrder.Count`), builtin types included | 46043 |
| Children of the root, in order | `/world`, `/savefile`, `/datum`, `/alist`, `/callee`, `/list`, `/vector` |
| Deepest type (levels below the root) | 13 |
| Types whose parent is not their path prefix (one-segment types under `/datum`, `parent_type`) | 20 |
| Explicit `parent_type` in tg (production build) | `/client` -> `/datum`, `/datum/tgs_api/latest` -> `/datum/tgs_api/v5` |
| Types never defined (their explicit parent does not exist) / not reachable from the root | 0 / 0 |
| `ProcOrder`: types tg enumerates with `typesof(/t/proc)` or `typesof(/t/verb)` | `/datum/controller/global_vars/proc` (1088) |

Where a type sits among its siblings when its parent is not its path prefix (position among the parent's children, from 1):

| Parent | Children | Non-prefix children and their positions |
|---|---|---|
| `/datum` | 604 | `/atom` 591, `/client` 592, `/procpath` 593, `/generator` 594, `/icon` 595, `/image` 596, `/matrix` 597, `/particles` 598, `/regex` 599, `/database` 600, `/exception` 601, `/dm_filter` 602, `/pixloc` 603, `/sound` 604 |
| `/datum/tgs_api/v5` | 1 | `/datum/tgs_api/latest` 1 |
| `/atom` | 3 | `/turf` 2, `/area` 3 |
| `/atom/movable` | 15 | `/mob` 14, `/obj` 15 |
| `/image` | 2 | `/mutable_appearance` 2 |
<!-- generated:typeorder:end -->

## Slot registry (`slots.tsv`)

translation-rules.md 7.1 fixes a slot's C# signature once, from every definition of the slot, by rules 3.3, 7.2 and 7.4; `slots.tsv` is that registry for every slot with tg definitions and is authoritative, and translation-rules.md Appendix A is an excerpt of it (the M0 base slots) plus the per-slot decisions the generator does not make: a narrowed return type and a parameter decided from the `undecided` column (7.1). `tools/coloring/slots.py` computes it from the same parse as the colouring:

- **Parameters** (3.3). The root definition fixes names and types: the tg definition on the root type (the first one when there are two), or, for a BYOND builtin root that tg does not define there (`/datum/New`, `/atom/Enter`, ...), the parameters of the BYOND reference entry. P1 typed: the class (2.3, with B.1's `__2`), `DmList` for `list/x` and `x[]`; P2 untyped without a default or `= null`: `object`; P3 untyped with a numeric constant default: `float?` (the default after macro expansion is made of numbers, operators and BYOND's builtin numeric constants such as `TRUE`, `FALSE`, `NORTH`); P4 a text literal: `string`; P5 any other default: the P1/P2 type; an untyped parameter with a typepath default is `Type` (V3, which P1 applies to typed parameters; a gap in P2-P5). A proc reference is text, not a typepath (translation-rules.md 7.7): a default that is a proc or verb path (`/proc/x`, `/verb/x`, `/t/proc/x`; `GLOBAL_PROC_REF(x)` expands to `/proc/x`) or the `nameof(...)` that `PROC_REF`, `VERB_REF`, `TYPE_PROC_REF` and `TYPE_VERB_REF` expand to counts as a text default (P4: `string`, noted `string (proc reference default, 7.7)`), so `sortTim(list/to_sort, cmp = GLOBAL_PROC_REF(cmp_numeric_asc))` takes `string cmp`. Every parameter is `= null`.
- **`Type` (P1, V3).** A typed parameter is `Type` when some definition of the slot gives it a typepath default (never a proc path, above) or uses it as a type in `new P` or `typesof(P)`. `ispath(P)` is a type test (below), not a use as a type: a parameter that tg tests with `ispath` holds a path or an instance. When that test leads to no 3.4 verdict (no read follows on the typepath side), the parameter is listed as undecided (rules 3.3 P1), since the value there is a typepath (`/area/shuttle/place_on_top_react` tests `ispath(added_layer, /turf/open/floor/plating)`, `areas/shuttles.dm:23`, and every caller passes a turf type). `initial(P.v)` and `P::v` read an instance as well as a type (10.3), so they are listed as undecided and the declared type is kept.
- **Typed parameters filled with other kinds** (3.4). Over every definition of the slot (each with its own name for the parameter at that position), a type test (`istype`, `islist`, `isnum`, `istext`, `ispath`, `astype`, BYOND's and tg's `is*`) splits the paths: the test shows "another kind" on its failing branch when it tests the declared type or an ancestor (`istype(P)` tests the type that definition declares), on its succeeding branch when it tests a disjoint kind (`isnum`, a type that is neither an ancestor nor a descendant), and on neither when it tests a descendant. The parameter is `object` when the definition reads it on such a path, other than in another type test or a null test (`isnull`, `== null`, `!P`, `if(P)`): in a later term of the condition that the path evaluates, in a branch of the `if` that only the other-kind path takes, or after the `if` when the other-kind path falls through and the declared-type path cannot (every branch it takes ends in `return`, `continue`, `break`, `CRASH` or `throw`). An assignment `P = ...` that does not read `P` ends the path. Which branches a path takes follows DM's short-circuit evaluation of the condition's terms: a null test of `P` has the same value for every non-null value (null fits every declared type, so 3.4 is about non-null values of another kind), `P` alone is true for an object, and another type test of `P` has a known value for a value of the declared type (true for that type or an ancestor, false for a disjoint type or a non-object kind); every other term can go either way. So `if(!isnull(P) && !ispath(P)) return` sends every typepath past the `if`, and `istext(P) || ispath(P, /t)` keeps a value of the declared type out of the body. Code that both paths reach does not tell the kinds apart and is not counted (the frequent `if(istype(user) && ...)` null guard, or the body of `if(A || !istype(P))`, which `A` can open for any `P`). A read that only feeds a `CRASH`, `stack_trace` or `log_*` message would make the parameter `object` under 3.4 read literally, but changes only the text of that message: such parameters keep the declared type and are listed as undecided. So are tests the regex front end cannot follow: a test nested inside a larger expression or condition (mixed `&&` and `||`), outside an `if` condition (a ternary, an assignment, a loop header), or against a computed type.
- **Defaults of another kind** (3.5). When a default at a position has a kind the root's parameter cannot hold, the parameter is `object`: for an untyped parameter typed by its root's default (P3, P4, a typepath), an override default of another known kind (a text default where the root's is numeric, a number where it is a typepath); for a typed parameter, a text or numeric default in any definition. A typepath default makes a typed parameter `Type` instead (P1 above).
- **Variadic** (7.2, 7.6): the root declares `...`, some definition reads `args`, or an override declares more parameters than the root.
- **Return type** (7.4): `void` when no definition returns a value of its own (`return <expr>` other than `return ..()`, `return .` and `return null`, or an assignment to `.` other than `. = ..()`, outside detached code) and the BYOND builtin root, if any, returns nothing (its reference entry has no "Returns"); `/datum/New` is always `void`. A `return null` is the value a caller of a `void` slot gets anyway (7.4), so it does not make the slot `object`. Narrowing to `float`, `string` or a class stays a decision recorded per slot in Appendix A.
- **`DmTask`** exactly when `suspends = yes` (the rule above).
- **Names** (2.2, 2.4, 7.1): C# keywords get `@`; a proc whose name is also a var declared on the same type or along one ancestor line (tg vars and BYOND builtin vars) is renamed `<name>__proc`; a builtin root whose class inherits another slot of the same name (`/client` under tg's `parent_type = /datum`) is `public new virtual`.

Columns: `root_path`, `name`, `cs_name` (the C# method name), `root_def` (file:line of the root definition, or `builtin`), `dm_params` (the root's DM parameters as written), `variadic` (`no`, or why: `root ...`, `args`, `extra params`), `returns` (`void` or `object`, before `DmTask`), `colored` and `suspends` (as in `colored-slots.tsv`), `signature` (the root's C# declaration; overrides use `override` and the same parameters), `extra_param_overrides` (overrides with more DM parameters than the root), `dmparams_overrides` (overrides that need `[DmParams]`), `waitfor` (the root's own `set waitfor = FALSE` and the overrides that set it themselves; `waitfor_false` is the effective value over all definitions), `defs`, `notes` (why a parameter is not its P1-P5 type), `undecided` (parameters the scanner cannot decide; they keep their declared type).

Limits: the analysis is textual (regex front end, one body at a time). It does not see a value of another kind that tg stores into a typed parameter without testing it (3.5's "-" cells found at translation time), and it does not narrow return types.

### Slot registry (generated)

<!-- generated:slots:begin -->
| Quantity | Value |
|---|---|
| Slots with tg definitions (rows of `slots.tsv`): on types / global procs | 25345: 23749 / 1596 |
| Return type `object` / `void` (uncoloured) | 9407 / 15938 |
| Coloured / with callers that must wait (`suspends`: the signature is `DmTask` or `DmTask<object>`) | 8028 / 7929 |
| Variadic (`params object[] __extra`: root `...`, a definition reads `args`, or an override adds parameters) | 549 |
| Roots that are BYOND builtins without a tg definition on the root type (parameters from the BYOND reference) | 12 |
| Parameters: class / `DmList` / `Type` / `float?` / `string` / `object` | 17991 / 2252 / 77 / 3086 / 270 / 10246 |
| Typed parameters retyped `object` by 3.4 (type-tested, read where the test showed another kind) | 183 |
| Parameters made `Type` (typepath default, `new P`, `typesof(P)`) | 77 |
| Parameters made `string` by a proc reference default (`/proc/x`, `GLOBAL_PROC_REF`, `PROC_REF`; 7.7) | 9 |
| Parameters retyped `object` by a default of another kind in an override (3.5) | 3 |
| Undecided parameters (kept as declared; column `undecided`): 3.4 cases the scanner cannot follow / read only in a CRASH, stack_trace or log message / `initial(P.v)` or `P::v` / `ispath(P)` with no 3.4 verdict | 341 / 23 / 196 / 4 (in 534 slots) |
| Procs renamed `<name>__proc` (2.4: a var of the same name on one ancestor line) | 83 slots, 78 names |
| Builtin roots that hide an inherited slot (`public new virtual`, 7.1) | 3 |
| Class-name collisions (2.3, B.1) | 2: `/datum/action/innate/mimic_lock` / `/datum/action/innate/mimic/lock`, `/obj/projectile/energy/tesla_cannon` / `/obj/projectile/energy/tesla/cannon` |
| Appendix A rows checked / differences | 36 / 4 |

Compared with translation-rules.md Appendix B.2 (70 names, a regex scan that includes `UNIT_TESTS` code): not renamed here (their var or proc exists only under `UNIT_TESTS`): `attack_hit`, `completed`, `post_attack_hit`, `pre_attack_hit`, `sig_caught`, `started`; renamed here and missing from B.2: `allowed`, `deafness_check`, `drop`, `emergency_access_cooldown`, `inflation_value`, `kick`, `light`, `run_experiment`, `say_mod`, `should_refresh`, `slam`, `smooth_icon`, `strike`, `throw_wrassle`.
<!-- generated:slots:end -->

### Appendix A cross-check (generated)

The rows of translation-rules.md Appendix A compared with `slots.tsv` (signatures uncoloured, as Appendix A writes them). Appendix A is not edited by the tool; a difference is a per-slot decision recorded there (a narrowed return type, a decided `undecided` parameter; translation-rules.md 7.1), a correction for Appendix A or a case for the scanner, decided by the lead.

<!-- generated:appendixa:begin -->
| Slot | Field | Appendix A | Generated (`slots.tsv`, uncoloured) |
|---|---|---|---|
| `/datum` `GetComponent` | signature | public virtual object GetComponent(Type c_type = null) | public virtual object GetComponent(datum_component c_type = null) |
| `/datum` `GetExactComponent` | signature | public virtual object GetExactComponent(Type c_type = null) | public virtual object GetExactComponent(datum_component c_type = null) |
| `/datum/controller/subsystem/processing/dcs` `add_to_arguments_that_are_lists` | signature | public virtual void add_to_arguments_that_are_lists(DmList argument = null, Type element_type = null) | public virtual void add_to_arguments_that_are_lists(DmList argument = null, datum_element element_type = null) |
| `/proc` `update_midnight_rollover` | signature | public static float update_midnight_rollover() | public static object update_midnight_rollover() |
<!-- generated:appendixa:end -->
