#!/usr/bin/env python3
"""Proc colouring scanner: which C# virtual slots of the tg tree must be async (DmTask).

Usage (from the repository root):

    python3 tools/coloring/color_procs.py            # regenerate docs/ai/specs/generated/* and
                                                     # the generated sections of
                                                     # docs/ai/specs/colored-procs.md
    python3 tools/coloring/color_procs.py --check    # exit 1 if the committed files are stale
    python3 tools/coloring/color_procs.py --stats    # print the summary only, write nothing

Options: --tg PATH (default: $ANOMALY_TGSTATION, else ../tgstation next to the repository or next
to the main checkout of a git worktree), --bound upper|lower, --define NAME[=VALUE] (a build
variant such as UNIT_TESTS; variants print their summary and write nothing)
(default upper; the committed list is the upper bound). Python 3 standard library only.
The method is documented in docs/ai/specs/colored-procs.md.
"""

import argparse
import collections
import os
import re
import subprocess
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import dmscan  # noqa: E402
import coloring  # noqa: E402
import typeorder  # noqa: E402
import slots  # noqa: E402

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
GEN_DIR = os.path.join('docs', 'ai', 'specs', 'generated')
SLOTS_TSV = os.path.join(GEN_DIR, 'colored-slots.tsv')
NOSLEEP_TSV = os.path.join(GEN_DIR, 'nosleep-sites.tsv')
NEW_TSV = os.path.join(GEN_DIR, 'sleeping-new.tsv')
DOC_MD = os.path.join('docs', 'ai', 'specs', 'colored-procs.md')
SLOT_REGISTRY_TSV = os.path.join(GEN_DIR, 'slots.tsv')
RULES_MD = os.path.join('docs', 'ai', 'translation-rules.md')  # read only (Appendix A and B.2)
CS_GEN_DIR = os.path.join('src', 'Core', 'Tg', 'Generated')
TYPE_ORDER_CS = os.path.join(CS_GEN_DIR, 'TypeOrder.g.cs')
PROC_ORDER_CS = os.path.join(CS_GEN_DIR, 'ProcOrder.g.cs')

# BYOND's DM_VERSION/DM_BUILD for the #if gates in tg (__byond_version_compat.dm; 516.1685 from tg's
# dependencies.sh, as the defines converter uses), CBT (tg's build passes it to every production compile,
# tools/build/build.ts:229; it only selects MAP_SWITCH/WHEN_COMPILE, map_switch.dm), and
# DISABLE_DREAMLUAU (translation-rules.md 1.7: tg's production configuration plus
# DISABLE_DREAMLUAU, as tg defines it for OpenDream at _compile_options.dm:133; the Lua admin
# scripting is not ported). Its body is empty, as in tg's own '#define DISABLE_DREAMLUAU'.
PREDEFINED = {'DM_VERSION': '516', 'DM_BUILD': '1685', 'CBT': '', 'DISABLE_DREAMLUAU': ''}
# Never expanded: the lint markers (expanded to nothing outside SpacemanDMM, the scanner reads
# them), CHECK_TICK, which never yields in the port (translation-rules.md 11.3, DEVIATIONS.md DV3), and
# TICK_CHECK, which is always false there (the scanner skips if(TICK_CHECK) bodies).
OPAQUE = ('SIGNAL_HANDLER', 'SHOULD_NOT_SLEEP', 'CHECK_TICK', 'CHECK_TICK_HIGH_PRIORITY',
          'TICK_CHECK', 'TICK_CHECK_HIGH_PRIORITY')

# The M0 base slots (ROADMAP M0) and notable defs reported in colored-procs.md.
M0_SLOTS = [
    ('datum New', ('/datum', 'New')),
    ('datum Destroy (also atom Destroy)', ('/datum', 'Destroy')),
    ('datum Initialize', ('/datum', 'Initialize')),
    ('component Initialize', ('/datum/component', 'Initialize')),
    ('atom Initialize', ('/atom', 'Initialize')),
    ('atom LateInitialize', ('/atom', 'LateInitialize')),
    ('atom Entered', ('/atom', 'Entered')),
    ('atom Exited', ('/atom', 'Exited')),
    ('atom Enter', ('/atom', 'Enter')),
    ('atom Exit', ('/atom', 'Exit')),
    ('movable Bump', ('/atom/movable', 'Bump')),
    ('atom Bumped', ('/atom', 'Bumped')),
    ('atom update_icon', ('/atom', 'update_icon')),
    ('atom update_appearance', ('/atom', 'update_appearance')),
    ('atom setDir', ('/atom', 'setDir')),
    ('atom set_light', ('/atom', 'set_light')),
    ('movable Move', ('/atom/movable', 'Move')),
    ('movable Moved', ('/atom/movable', 'Moved')),
    ('movable forceMove', ('/atom/movable', 'forceMove')),
    ('movable doMove', ('/atom/movable', 'doMove')),
    ('movable moveToNullspace', ('/atom/movable', 'moveToNullspace')),
    ('process', ('/datum', 'process')),
    ('Life', ('/mob/living', 'Life')),
    ('subsystem fire', ('/datum/controller/subsystem', 'fire')),
    ('subsystem ignite', ('/datum/controller/subsystem', 'ignite')),
    ('controller Initialize', ('/datum/controller', 'Initialize')),
    ('callback Invoke', ('/datum/callback', 'Invoke')),
    ('callback InvokeAsync', ('/datum/callback', 'InvokeAsync')),
    ('do_after', ('', 'do_after')),
    ('stoplag', ('', 'stoplag')),
    ('client New', ('/client', 'New')),
]
# M0 translation base files (ROADMAP M0): per-file colouring and NoSleep counts.
M0_FILES = [
    'code/datums/datum.dm',
    'code/datums/signals.dm',
    'code/datums/callback.dm',
    'code/datums/timed_actions.dm',
    'code/datums/components/_component.dm',
    'code/datums/elements/_element.dm',
    'code/game/atom/_atom.dm',
    'code/game/atom/atoms_initializing_EXPENSIVE.dm',
    'code/game/atom/atom_appearance.dm',
    'code/game/atom/atom_color.dm',
    'code/game/atoms_movable.dm',
    'code/game/area/areas.dm',
    'code/game/turfs/turf.dm',
    'code/game/turfs/open/_open.dm',
    'code/game/objects/objs.dm',
    'code/modules/mob/mob.dm',
    'code/controllers/controller.dm',
    'code/controllers/master.dm',
    'code/controllers/failsafe.dm',
    'code/controllers/subsystem.dm',
    'code/controllers/subsystem/timer.dm',
    'code/controllers/subsystem/processing/processing.dm',
    'code/controllers/subsystem/garbage.dm',
    'code/__HELPERS/stoplag.dm',
]

M0_DEFS = [
    '/datum/controller/master/Initialize',
    '/datum/controller/failsafe/Initialize',
    '/datum/controller/subsystem/Initialize',
    '/datum/controller/master/Loop',
    '/datum/controller/master/RunQueue',
    '/datum/controller/subsystem/fire',
    '/datum/controller/subsystem/processing/fire',
    '/datum/controller/subsystem/timer/fire',
    '/datum/controller/subsystem/machines/fire',
    '/datum/controller/subsystem/mobs/fire',
    '/atom/movable/Move',
    '/atom/movable/Moved',
    '/atom/movable/forceMove',
    '/atom/movable/doMove',
    '/atom/Entered',
    '/atom/Exited',
    '/turf/Enter',
    '/area/Entered',
    '/atom/update_icon',
    '/atom/setDir',
    '/atom/set_light',
    '/datum/New',
    '/atom/New',
    '/datum/Destroy',
    '/atom/Destroy',
    '/proc/qdel',
    '/datum/_SendSignal',
]


def default_tg():
    """The tg clone next to the repository: $ANOMALY_TGSTATION, ../tgstation, or ../tgstation
    next to the main checkout when running in a git worktree."""
    env = os.environ.get('ANOMALY_TGSTATION')
    if env:
        return env
    cand = os.path.normpath(os.path.join(REPO, '..', 'tgstation'))
    if os.path.exists(os.path.join(cand, 'tgstation.dme')):
        return cand
    try:
        out = subprocess.run(['git', '-C', REPO, 'rev-parse', '--path-format=absolute',
                              '--git-common-dir'], capture_output=True, text=True, timeout=20)
        if out.returncode == 0:
            main = os.path.dirname(out.stdout.strip())
            alt = os.path.normpath(os.path.join(main, '..', 'tgstation'))
            if os.path.exists(os.path.join(alt, 'tgstation.dme')):
                return alt
    except (OSError, subprocess.SubprocessError):
        pass
    return cand


def tg_revision(tg):
    try:
        out = subprocess.run(['git', '-C', tg, 'rev-parse', '--short=8', 'HEAD'],
                             capture_output=True, text=True, timeout=20)
        if out.returncode == 0:
            return out.stdout.strip()
    except (OSError, subprocess.SubprocessError):
        pass
    return 'unknown'


def analyze(tg, bound='upper', defines=None):
    """Parse the tg tree and run the colouring. Returns (Analysis, Preprocessor, timings)."""
    t0 = time.time()
    predefined = dict(PREDEFINED)
    predefined.update(defines or {})
    parse, pre = dmscan.parse_tree(tg, 'tgstation.dme', predefined, OPAQUE)
    t1 = time.time()
    # The comparison run first: an Analysis keeps its results on the parse's Def objects, and
    # the last one constructed over a parse owns them.
    cmp_run = coloring.Analysis(parse, coloring.Options(bound=bound, dispatch='slot')).run()
    a = coloring.Analysis(parse, coloring.Options(bound=bound)).run()
    a.slot_dispatch_counts = cmp_run.counts
    a.tg = tg
    t2 = time.time()
    return a, pre, (t1 - t0, t2 - t1)


# ------------------------------------------------------------------------------------------------
# Formatting
# ------------------------------------------------------------------------------------------------

def yn(b):
    return 'yes' if b else 'no'


def all_some_no(flags):
    if not flags:
        return 'no'
    n = sum(1 for f in flags if f)
    if n == 0:
        return 'no'
    return 'yes' if n == len(flags) else 'some'


def slot_flags(a, s):
    tg = s.tg_defs()
    lower = any(d.phase == 1 for d in s.defs)
    return {
        'colored': s.colored,
        'suspends': s.suspends,
        'lower': lower,
        'waitfor_false': all_some_no([not d.wf for d in tg]),
        'sync_mandated': all_some_no([d.sm for d in tg]),
        'defs': len(tg),
        'sleeping': sum(1 for d in s.defs if d.layer is not None),
    }


def tsv_cell(x):
    return str(x).replace('\t', ' ').replace('\n', ' ')


def short_witness(a, s):
    """The witness chain of slot s for colored-slots.tsv: the chain of Analysis.chain() up to
    the first later hop D that starts the witness of another slot T, written 'D [T]'; the chain
    goes on as the witness of T (its own row). Chains are shortest paths, so D is nearer to the
    primitive than the first hop and following the references always ends at a primitive."""
    d = a.slot_witness_def(s)
    if d is None:
        return ''
    parts = []
    first = True
    limit = 80
    while d is not None and limit:
        limit -= 1
        if not first:
            t = d.slot
            if t is not s and a.slot_witness_def(t) is d:
                parts.append('%s [%s]' % (d.display(), t.display()))
                break
        first = False
        w = d.witness
        if w is None:
            parts.append(d.display() + ' (?)')
            break
        if w[0] == 'builtin':
            parts.append(d.display() + ' (builtin, sleeps)')
            break
        parts.append('%s (%s:%d)' % (d.display(), d.file, w[1]) if not d.builtin
                     else '%s (builtin)' % d.display())
        if w[0] == 'seed':
            parts.append(w[2])
            break
        if w[0] == 'dyn':
            parts.append('call()() ' + ' '.join(w[2].split()))
            break
        if w[0] == 'super':
            parts.append('..()')
        d = w[2]
    return ' -> '.join(parts)


def expand_witness(rows_by_slot, witness, limit=200):
    """The full chain of a colored-slots.tsv witness: each 'D [T]' hop replaced by the witness
    of T (used by the self-check)."""
    out = witness
    while limit:
        limit -= 1
        m = re.search(r'(\S+) \[([^\]]+)\]$', out)
        if not m:
            return out
        ref = rows_by_slot.get(m.group(2))
        if ref is None or not ref.startswith(m.group(1) + ' '):
            return None
        out = out[:m.start()] + ref
    return None


def slots_rows(a):
    rows = []
    for s in a.slots.values():
        tg = s.tg_defs()
        if not tg and not s.call_sites and not s.union_sites:
            continue
        f = slot_flags(a, s)
        rows.append((s.root(), s.key[1], yn(f['colored']), f['waitfor_false'], f['sync_mandated'],
                     str(f['defs']), str(s.call_sites),
                     short_witness(a, s) if s.colored else '',
                     yn(f['suspends']), yn(f['lower']), str(s.union_sites), str(f['sleeping'])))
    rows.sort(key=lambda r: (r[0], r[1]))
    return rows


SLOTS_HEADER = ('root_path', 'name', 'colored', 'waitfor_false', 'sync_mandated', 'defs',
                'call_sites', 'witness', 'suspends', 'lower_bound', 'union_sites', 'sleeping_defs')


def callee_text(a, site, targets):
    if site.kind == 'super':
        return targets[0][2].display()
    keys = []
    for key, _, _ in targets:
        disp = a.slots[key].display()
        if disp not in keys:
            keys.append(disp)
    return ' | '.join(keys)


def call_text(site):
    if site.kind == 'super':
        return '..()'
    if site.kind in ('new', 'new-typed', 'new-untyped'):
        return 'new'
    if site.kind == 'handler':
        return 'RegisterSignal ' + site.text
    if site.kind in ('dynamic', 'dyn-known', 'dyn-self'):
        return 'call()()'
    return site.text


def nosleep_rows(a):
    merged = collections.OrderedDict()
    for site, ctx, targets, may in a.nosleep_sites():
        d = site.caller
        callee = callee_text(a, site, targets) if targets else site.text
        key = (d.file, site.line, d.display(), call_text(site), callee, ctx)
        merged[key] = merged.get(key, False) or may
    rows = [('%s:%d' % (k[0], k[1]), k[2], k[3], k[4], yn(v), k[5]) for k, v in merged.items()]
    rows.sort(key=lambda r: (r[0].rsplit(':', 1)[0], int(r[0].rsplit(':', 1)[1]), r[1], r[2], r[3]))
    return rows


NOSLEEP_HEADER = ('site', 'caller', 'call', 'callee', 'may_suspend', 'context')


def new_rows(a):
    rows = []
    s = a.slots[('/datum', 'New')]
    for d in s.defs:
        if d.builtin or d.layer is None:
            continue
        rows.append((d.type, '%s:%d' % (d.file, d.line), yn(d.wf), yn(d.phase == 1), a.chain(d)))
    rows.sort()
    return rows


NEW_HEADER = ('type', 'definition', 'suspends', 'lower_bound', 'witness')


def render_tsv(header, rows):
    out = ['\t'.join(header)]
    for r in rows:
        out.append('\t'.join(tsv_cell(x) for x in r))
    return '\n'.join(out) + '\n'


def summary_md(a, pre, tg_rev, slot_rows, ns_rows, nrows):
    tg_defs = [d for d in a.defs if not d.builtin]
    slots = [s for s in a.slots.values() if s.tg_defs()]
    col = [s for s in slots if s.colored]
    low = [s for s in slots if any(d.phase == 1 for d in s.defs)]
    sus = [s for s in slots if s.suspends]
    sleeping = [d for d in tg_defs if d.layer is not None]
    sleeping_low = [d for d in sleeping if d.phase == 1]
    in_col = sum(len(s.tg_defs()) for s in col)
    in_sus = sum(len(s.tg_defs()) for s in sus)
    sm = sum(1 for d in tg_defs if d.sm)
    wff = sum(1 for d in tg_defs if not d.wf)
    ctx = collections.Counter((r[5], r[4]) for r in ns_rows)
    kinds = a.kind_counts
    names_col = len({s.key[1] for s in col})
    p = a.p
    lines = []
    w = lines.append
    w('| Quantity | Value |')
    w('|---|---|')
    w('| tg snapshot | `%s` |' % tg_rev)
    w('| Files parsed (included by tgstation.dme, recursively) | %d |' % len(pre.files))
    w('| Lines parsed | %d |' % pre.total_lines)
    w('| Proc definitions (procs and verbs, without builtins) | %d |' % len(tg_defs))
    w('| Slots with tg definitions (root path + name) | %d |' % len(slots))
    label = ('Coloured slots, upper bound (the committed list)' if a.opt.bound == 'upper'
             else 'Coloured slots (this run stops at the lower bound)')
    w('| %s | %d (%d distinct names) |' % (label, len(col), names_col))
    w('| Coloured slots, lower bound (typed calls only) | %d |' % len(low))
    w('| Coloured slots whose callers can be suspended (`suspends`) | %d |' % len(sus))
    sd = getattr(a, 'slot_dispatch_counts', None)
    if sd is not None:
        w('| Comparison: coloured slots / definitions that may sleep if every call reached every definition of the callee slot (no dispatch narrowing) | %d / %d |'
          % sd)
    w('| Definitions in coloured slots | %d (%.1f%%) |' % (in_col, 100.0 * in_col / len(tg_defs)))
    w('| Definitions in slots that suspend (`DmTask` signature, waiting-model.md 4.2) | %d (%.1f%%) |' % (in_sus, 100.0 * in_sus / len(tg_defs)))
    w('| Definitions that may sleep, upper / lower bound | %d / %d |' % (len(sleeping), len(sleeping_low)))
    w('| Sync-mandated definitions (SIGNAL_HANDLER, SHOULD_NOT_SLEEP, own or inherited) | %d |' % sm)
    w('| Definitions with effective `waitfor = FALSE` (own or inherited) | %d |' % wff)
    w('| Overriding `New` definitions that may sleep (`sleeping-new.tsv`) | %d |' % len(nrows))
    w('| NoSleep sites (`nosleep-sites.tsv`) | %d |' % len(ns_rows))
    for c in ('SIGNAL_HANDLER', 'SHOULD_NOT_SLEEP', 'sync', 'RegisterSignal'):
        w('| &nbsp;&nbsp;context `%s`: may suspend / always completed | %d / %d |' % (c, ctx[(c, 'yes')], ctx[(c, 'no')]))
    w('| Direct sleep-primitive sites (seeds) | %d |' % kinds['seed'])
    w('| `call()()` sites: unknown target (count as a sleep) / resolved by convention | %d / %d |'
      % (kinds['dynamic'], kinds['dyn-known'] + kinds['dyn-signal'] + kinds['dyn-self']))
    w('| Calls: unqualified on src / global / typed receiver | %d / %d / %d |' % (kinds['self'], kinds['global'], kinds['typed']))
    w('| Calls: untyped receiver / typed receiver without the proc / below the receiver type / unresolved name | %d / %d / %d / %d |'
      % (kinds['untyped'], kinds['typed-miss'], kinds['typed-down'], kinds['unresolved']))
    w('| `new`: static type / bounded computed type / unknown computed type | %d / %d / %d |'
      % (kinds['new'], kinds['new-typed'], kinds['new-untyped']))
    w('| `..()` calls / builtin-proc calls / step() calls | %d / %d / %d |' % (kinds['super'], kinds['builtin'], kinds['step']))
    w('| Unparsed type-level items / unclosed brace blocks / implicit roots (override with no declaration found) | %d / %d / %d |'
      % (len(p.unparsed), p.unclosed_braces, len(a.orphans)))
    w('| Active `#error` directives / missing includes | %d / %d |' % (len(pre.errors), len(pre.missing)))
    return '\n'.join(lines)


def m0_md(a):
    lines = []
    w = lines.append
    w('| M0 proc | Slot (root path / name) | Coloured | Suspends callers | waitfor FALSE | Sync-mandated | Lower bound | Defs | Call sites | Witness chain |')
    w('|---|---|---|---|---|---|---|---|---|---|')
    for label, key in M0_SLOTS:
        s = a.slots.get(key)
        if s is None:
            w('| %s | `%s/%s`: no such slot (no root declaration there) | | | | | | | | |' % (label, key[0] or '/proc', key[1]))
            continue
        f = slot_flags(a, s)
        wit = a.slot_witness(s) if s.colored else ''
        w('| %s | `%s` | %s | %s | %s | %s | %s | %d | %d | %s |' % (
            label, s.display(), yn(f['colored']), yn(f['suspends']), f['waitfor_false'],
            f['sync_mandated'], yn(f['lower']), f['defs'], s.call_sites,
            md_code(wit) if wit else ''))
    w('')
    w('Notable definitions (the def itself, not its slot):')
    w('')
    w('| Definition | Slot | May sleep | waitfor FALSE | Sync-mandated | Witness chain |')
    w('|---|---|---|---|---|---|')
    by_display = collections.defaultdict(list)
    for d in a.defs:
        by_display[d.display()].append(d)
    for disp in M0_DEFS:
        ds = by_display.get(disp)
        if not ds:
            w('| `%s` | (not found) | | | | |' % disp)
            continue
        d = ds[-1]  # the definition that dispatch reaches (the last one at its type)
        where = 'builtin' if d.builtin else '%s:%d' % (d.file, d.line)
        w('| `%s` (%s) | `%s` | %s | %s | %s | %s |' % (
            disp, where, d.slot.display(), yn(d.layer is not None), yn(not d.wf),
            yn(d.sm), md_code(a.chain(d)) if d.layer is not None else ''))
    return '\n'.join(lines)


def seeds_md(a, top=30):
    """The sleep primitives tg uses, and the defs that call one directly, by call sites."""
    prim = collections.Counter()
    for d, _, p in a.seed_sites:
        prim[p] += 1
    for d in a.defs:
        if d.builtin and d.seed_builtin:
            prim['%s.%s' % (d.type.strip('/'), d.name)] += a.slots[d.slot.key].call_sites + a.slots[d.slot.key].union_sites
    lines = []
    w = lines.append
    w('Builtin primitives (seeds) and their call sites: ' + ', '.join(
        '`%s` %d' % (k, v) for k, v in sorted(prim.items(), key=lambda kv: (-kv[1], kv[0])) if v) + '.')
    w('`call()()` with an unknown target counts as a sleep: %d sites in %d defs (e.g. `/datum/callback/Invoke`).'
      % (len(a.dyn_sites), len({d.idx for d, _, _ in a.dyn_sites})))
    w('')
    w('tg procs that sleep directly (their own body calls a primitive, outside spawn/ASYNC), by call sites:')
    w('')
    w('| Proc | Primitive | Slot coloured | Suspends callers | Call sites |')
    w('|---|---|---|---|---|')
    direct = {}
    for d, line, p in a.seed_sites:
        if not d.sm and d.idx not in direct:
            direct[d.idx] = (d, p)
    rows = []
    for d, p in direct.values():
        s = d.slot
        rows.append((-(s.call_sites + s.union_sites), d.display(), p, s, d))
    rows.sort(key=lambda r: (r[0], r[1]))
    for neg, disp, p, s, d in rows[:top]:
        w('| `%s` (%s:%d) | `%s` | %s | %s | %d |' % (disp, d.file, d.line, p, yn(s.colored), yn(d.wf), -neg))
    w('')
    w('%d defs sleep directly; %d more are coloured through calls (the full list is the `colored = yes` rows of `colored-slots.tsv`).'
      % (len(direct), sum(1 for d in a.defs if not d.builtin and d.layer is not None) - len(direct)))
    return '\n'.join(lines)


def m0_files_md(a, ns_rows):
    per = collections.OrderedDict((f, collections.Counter()) for f in M0_FILES)
    for d in a.defs:
        c = per.get(d.file)
        if c is None:
            continue
        c['defs'] += 1
        if d.slot.suspends:
            c['dmtask'] += 1
        if d.layer is not None:
            c['sleeps'] += 1
        if d.sm:
            c['sm'] += 1
    for row in ns_rows:
        c = per.get(row[0].rsplit(':', 1)[0])
        if c is not None:
            c['ns_yes' if row[4] == 'yes' else 'ns_no'] += 1
    lines = ['| tg file | Defs | In slots that suspend (DmTask) | May sleep | Sync-mandated | NoSleep sites: may suspend / completed |',
             '|---|---|---|---|---|---|']
    for f, c in per.items():
        lines.append('| `%s` | %d | %d | %d | %d | %d / %d |' % (
            f, c['defs'], c['dmtask'], c['sleeps'], c['sm'], c['ns_yes'], c['ns_no']))
    return '\n'.join(lines)


def conflicts_md(a, ns_rows, top=20):
    """The callees of most NoSleep conflicts (may_suspend = yes in a sync-mandated caller)."""
    cnt = collections.Counter()
    for row in ns_rows:
        if row[4] == 'yes' and row[5] in ('SIGNAL_HANDLER', 'SHOULD_NOT_SLEEP'):
            for callee in row[3].split(' | '):
                cnt[callee] += 1
    by_disp = {s.display(): s for s in a.slots.values()}
    lines = ['| Callee slot | Conflict sites | Witness chain |', '|---|---|---|']
    for callee, n in sorted(cnt.items(), key=lambda kv: (-kv[1], kv[0]))[:top]:
        s = by_disp.get(callee)
        wit = a.slot_witness(s) if s is not None else ''
        lines.append('| `%s` | %d | %s |' % (callee, n, md_code(wit) if wit else ''))
    return '\n'.join(lines)


def md_code(s):
    return '`' + s.replace('`', "'").replace('|', '\\|') + '`'


def replace_block(text, name, body):
    begin = '<!-- generated:%s:begin -->' % name
    end = '<!-- generated:%s:end -->' % name
    i = text.find(begin)
    j = text.find(end)
    if i < 0 or j < 0 or j < i:
        raise SystemExit('%s: missing the %s / %s markers' % (DOC_MD, begin, end))
    return text[:i + len(begin)] + '\n' + body + '\n' + text[j:]


def type_tree(a):
    """The DM type tree of the parse (typeorder.py), built once per Analysis."""
    tree = getattr(a, 'type_tree', None)
    if tree is None:
        tree = typeorder.TypeTree(a.p.types, a.p.parent_type_decls)
        a.type_tree = tree
    return tree


def slot_registry(a):
    """The slot registry of the parse (slots.py), built once per Analysis."""
    reg = getattr(a, 'slot_registry', None)
    if reg is None:
        reg = slots.Registry(a, type_tree(a))
        a.slot_registry = reg
    return reg


def read_rules():
    path = os.path.join(REPO, RULES_MD)
    if not os.path.exists(path):
        return ''
    with open(path, encoding='utf-8') as f:
        return f.read()


def outputs(a, pre, tg_rev):
    """{repo-relative path: content} for every generated file."""
    srows = slots_rows(a)
    nrows_sites = nosleep_rows(a)
    nrows = new_rows(a)
    tree = type_tree(a)
    procs = typeorder.proc_order(a.defs, typeorder.enumerated_proc_types(a.defs))
    reg = slot_registry(a)
    rules = read_rules()
    reg_rows = slots.tsv_rows(reg, slots.RawHeaders(a.tg), lambda s: slot_flags(a, s))
    diffs, n_appendix = slots.cross_check(reg, a, rules)
    renamed = sorted({sig.slot.key[1] for sig in reg.sigs.values() if sig.cs_name.endswith('__proc')})
    files = {
        SLOTS_TSV: render_tsv(SLOTS_HEADER, srows),
        NOSLEEP_TSV: render_tsv(NOSLEEP_HEADER, nrows_sites),
        NEW_TSV: render_tsv(NEW_HEADER, nrows),
        SLOT_REGISTRY_TSV: render_tsv(slots.SLOTS_TSV_HEADER, reg_rows),
        TYPE_ORDER_CS: typeorder.render_type_order(tree, tg_rev),
        PROC_ORDER_CS: typeorder.render_proc_order(procs, tg_rev),
    }
    summary = summary_md(a, pre, tg_rev, srows, nrows_sites, nrows)
    doc_path = os.path.join(REPO, DOC_MD)
    if os.path.exists(doc_path):
        with open(doc_path, encoding='utf-8') as f:
            doc = f.read()
        doc = replace_block(doc, 'summary', summary)
        doc = replace_block(doc, 'seeds', seeds_md(a))
        doc = replace_block(doc, 'm0', m0_md(a))
        doc = replace_block(doc, 'm0files', m0_files_md(a, nrows_sites))
        doc = replace_block(doc, 'conflicts', conflicts_md(a, nrows_sites))
        doc = replace_block(doc, 'typeorder', typeorder.summary_md(tree, procs))
        doc = replace_block(doc, 'slots', slots.summary_md(reg, reg_rows, diffs, n_appendix,
                                                           slots.appendix_b2(rules), renamed))
        doc = replace_block(doc, 'appendixa', slots.diffs_md(diffs))
        files[DOC_MD] = doc
    return files, summary


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--tg', default=None, help='tgstation checkout (read-only); default: '
                    '$ANOMALY_TGSTATION or ../tgstation next to the repository')
    ap.add_argument('--bound', choices=('upper', 'lower'), default='upper')
    ap.add_argument('--define', action='append', default=[], metavar='NAME[=VALUE]',
                    help='an extra preprocessor define (e.g. UNIT_TESTS); prints, writes nothing')
    ap.add_argument('--check', action='store_true', help='compare with the committed files')
    ap.add_argument('--stats', action='store_true', help='print the summary, write nothing')
    args = ap.parse_args(argv)
    args.tg = args.tg or default_tg()
    if not os.path.exists(os.path.join(args.tg, 'tgstation.dme')):
        raise SystemExit('no tgstation.dme under %s (use --tg)' % args.tg)
    defines = {}
    for item in args.define:
        name, _, value = item.partition('=')
        defines[name] = value or '1'
    a, pre, (tp, ta) = analyze(args.tg, args.bound, defines)
    files, summary = outputs(a, pre, tg_revision(args.tg))
    print('parsed in %.1f s, analysed in %.1f s' % (tp, ta))
    print(summary)
    if args.stats:
        return 0
    if args.bound != 'upper' or defines:
        print('variant run: nothing written (the committed list is the upper bound of the '
              'production build)')
        return 0
    stale = []
    for rel, content in sorted(files.items()):
        path = os.path.join(REPO, rel)
        old = None
        if os.path.exists(path):
            with open(path, encoding='utf-8', newline='') as f:
                old = f.read()
        if old == content:
            continue
        stale.append(rel)
        if not args.check:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, 'w', encoding='utf-8', newline='\n') as f:
                f.write(content)
    if args.check:
        if stale:
            print('stale: ' + ', '.join(stale))
            return 1
        print('up to date')
        return 0
    print('written: ' + (', '.join(stale) if stale else 'nothing (up to date)'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
