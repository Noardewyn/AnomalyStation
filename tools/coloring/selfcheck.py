#!/usr/bin/env python3
"""Self-check of the colouring scanner: asserts facts verified by hand in the tg tree.

Usage (from the repository root): python3 tools/coloring/selfcheck.py [--tg PATH]
Runs in about 30 s (limit 60 s). Exit code 0 when every check passes. The colouring facts come
from the wave-0 review (findings W1, W2, W4, W9, K3) and were checked against the tg source by
the reviewers; the type-order facts follow object-model.md section 9 and OpenDream's sources,
the slot signatures translation-rules.md v1 (Appendix A and the rules' own examples).
"""

import argparse
import os
import re
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import color_procs  # noqa: E402
import coloring  # noqa: E402
import slots as slotreg  # noqa: E402
import typeorder  # noqa: E402

FAILS = []
PASSES = []


def check(cond, what):
    (PASSES if cond else FAILS).append(what)
    print(('ok   ' if cond else 'FAIL ') + what)


def main(argv=None):
    ap = argparse.ArgumentParser(description='colouring scanner self-check')
    ap.add_argument('--tg', default=None)
    args = ap.parse_args(argv)
    tg = args.tg or color_procs.default_tg()
    t0 = time.time()
    a, pre, _ = color_procs.analyze(tg)
    slots = a.slots
    defs_by = {}
    for d in a.defs:
        defs_by.setdefault(d.display(), []).append(d)

    def slot(root, name):
        return slots.get((root, name))

    def flags(root, name):
        s = slot(root, name)
        return color_procs.slot_flags(a, s) if s else None

    def d1(display):
        ds = [d for d in defs_by.get(display, ()) if not d.builtin]
        return ds[-1] if ds else None

    def sleeps(display):
        d = d1(display)
        return d is not None and d.layer is not None

    def nosleep(file, line, caller, call):
        for site, ctx, targets, may in a.nosleep_sites():
            if site.caller.file == file and site.line == line and site.caller.display() == caller \
                    and color_procs.call_text(site) == call:
                return ctx, may
        return None

    # -- the parse --------------------------------------------------------------------------
    check(len(pre.files) > 7000, 'parsed every file of tgstation.dme (%d)' % len(pre.files))
    check(sum(1 for d in a.defs if not d.builtin) > 60000, 'found the proc definitions')
    check(not a.p.unparsed and a.p.unclosed_braces == 0, 'no unparsed items, no unclosed brace blocks')
    check(not pre.errors and not pre.missing, 'no active #error, no missing include')
    check(not a.orphans, 'every override has a root declaration')

    # -- roots and slots (W9, K3) ---------------------------------------------------------------
    roots = sorted(s.root() for s in a.slots_by_name['Initialize'])
    check({'/atom', '/datum/controller', '/datum/component'} <= set(roots),
          'Initialize has separate roots per hierarchy: ' + ', '.join(roots))
    cn = slot('/client', 'New')
    dn = slot('/datum', 'New')
    check(cn is not None and dn is not None and cn is not dn,
          '/client/New is a slot of its own, separate from /datum/New')
    check(cn is not None and all(d.type == '/client' for d in cn.defs)
          and not any(d.type == '/client' for d in dn.defs),
          '/client New definitions live only in the /client/New slot')
    check(slot('/atom/movable', 'Move') is not None and slot('/atom', 'Entered') is not None
          and slot('/atom/movable', 'Bump') is not None,
          'atom builtins are roots where BYOND declares them (movable Move/Bump, atom Entered)')
    cd = d1('/client/MouseDrop')
    check(cd is not None and cd.layer is not None and cd.witness[0] == 'super',
          '/client/MouseDrop (drag_drop.dm:192) reaches /atom/MouseDrop through ..() and the builtin '
          'client/MouseDrop (BYOND default action: object.MouseDrop())')
    check(slot('/atom/movable', 'forceMove') is not None and slot('/atom', 'setDir') is not None,
          'tg declarations are roots (/atom/movable/forceMove, /atom/setDir)')

    # -- the master controller and the M0 lifecycle ------------------------------------------------
    f = flags('/datum/controller', 'Initialize')
    check(f['colored'], 'controller Initialize slot is coloured')
    m = d1('/datum/controller/master/Initialize')
    check(m is not None and m.layer is not None and not m.wf and m.own_waitfor is False,
          'master Initialize may sleep (sleep(delay)) and is waitfor = FALSE')
    check(m is not None and a.chain(m).endswith('master.dm:327) -> sleep'),
          'master Initialize witness is its own sleep(delay) at master.dm:327')
    f = flags('/atom', 'Initialize')
    check(not f['colored'] and f['sync_mandated'] == 'yes',
          '/atom/Initialize is not coloured and every override is sync-mandated (SHOULD_NOT_SLEEP)')
    f = flags('/datum', 'Destroy')
    check(not f['colored'] and f['sync_mandated'] == 'yes', '/datum/Destroy is not coloured, sync-mandated')
    f = flags('/atom', 'update_appearance')
    check(not f['colored'] and f['sync_mandated'] == 'yes', '/atom/update_appearance is not coloured, sync-mandated')
    f = flags('/mob/living', 'Life')
    check(not f['colored'] and f['sync_mandated'] == 'yes' and f['waitfor_false'] == 'yes',
          '/mob/living/Life is sync-mandated and waitfor = FALSE')

    # -- waiting helpers -------------------------------------------------------------------------
    for name in ('do_after', 'stoplag', 'tgui_alert', 'tgui_input_list'):
        f = flags('', name)
        check(f is not None and f['colored'] and f['suspends'], '/proc/%s is coloured and suspends its caller' % name)
    check(a.slot_witness(slot('', 'do_after')).endswith('-> sleep'), 'do_after witness ends in sleep')
    for disp in ('/obj/item/use_tool', '/datum/tgui_alert/wait'):
        d = d1(disp)
        check(d is not None and d.layer is not None and d.wf, '%s (a waiting helper) may sleep and suspends its caller' % disp)

    # -- tg wait macros are seen through their expansion; CHECK_TICK is not a wait ------------------------
    pm = d1('/datum/path_map/expand')
    check(pm is not None and pm.layer is not None and '/proc/stoplag' in a.chain(pm),
          'UNTIL (sssp.dm:148, /datum/path_map/expand) colours through stoplag')
    seeds = {(d.file, line, p) for d, line, p in a.seed_sites}
    check(('code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm', 27, 'sleep') in seeds,
          'SLEEP_CHECK_DEATH (tendril_actions.dm:27) is a sleep')
    check(('code/game/machinery/doors/door.dm', 598, 'sleep') in seeds, 'SLEEP_NOT_DEL (door.dm:598) is a sleep')
    raw = {}
    bad = []
    for st in a.sites:
        if st.kind == 'global' and st.text == 'stoplag':
            f = st.caller.file
            if f not in raw:
                with open(os.path.join(tg, f), encoding='utf-8', errors='replace') as fh:
                    raw[f] = fh.read().split('\n')
            src = raw[f][st.line - 1] if st.line - 1 < len(raw[f]) else ''
            if 'CHECK_TICK' in src and 'stoplag' not in src and 'UNTIL' not in src:
                bad.append('%s:%d' % (f, st.line))
    check(not bad, 'CHECK_TICK never counts as a stoplag call' + (' (%s)' % ', '.join(bad[:5]) if bad else ''))
    dead = [st.line for st in a.sites if st.caller.file == 'code/controllers/subsystem/atoms.dm'
            and st.text == 'stoplag' and st.line in (110, 131)]
    check(not dead, 'if(TICK_CHECK) bodies are dead code (the unrolled CHECK_TICK in atoms.dm:108-131)')
    lf = d1('/datum/controller/subsystem/lighting/fire')
    check(lf is not None and lf.layer is None and not any(
        st.caller is lf and st.text == 'stoplag' for st in a.sites),
        'the code after if(!TICK_CHECK) continue is dead (lighting.dm:70-74, 95-99, 122-126)')

    # -- waitfor = FALSE is a callee property (W4) ----------------------------------------------------
    f = flags('/datum', 'process')
    check(f['waitfor_false'] == 'yes' and f['colored'] and not f['suspends'],
          '/datum/process is waitfor = FALSE in every override, coloured, and never suspends callers')
    check(sleeps('/obj/machinery/computer/pod/process'),
          'pod.dm process sleeps through alarm() (the W4 example)')
    for root, name in (('/datum/controller/subsystem', 'ignite'), ('/datum/callback', 'InvokeAsync'),
                       ('/atom', 'Bumped'), ('/atom', 'LateInitialize')):
        f = flags(root, name)
        check(f['waitfor_false'] == 'yes' and not f['suspends'], '%s/%s is waitfor = FALSE and never suspends callers' % (root, name))
    f = flags('/atom', 'Entered')
    area = d1('/area/Entered')
    check(f['waitfor_false'] == 'some' and area is not None and not area.wf,
          '/atom/Entered is mixed: /area/Entered is waitfor = FALSE, other overrides are not')

    # -- the Move chain is coloured (W1) ------------------------------------------------------------------
    for root, name in (('/atom/movable', 'Move'), ('/atom/movable', 'Moved'), ('/atom/movable', 'forceMove'),
                       ('/atom/movable', 'doMove'), ('/atom/movable', 'moveToNullspace'),
                       ('/atom', 'Entered'), ('/atom', 'Exited'), ('/atom/movable', 'Bump'),
                       ('/atom', 'setDir'), ('/atom', 'update_icon'), ('/atom', 'set_light')):
        f = flags(root, name)
        check(f['colored'] and f['suspends'] and f['lower'],
              '%s/%s is coloured, suspends callers, and is coloured by the lower bound already' % (root, name))
    lf = d1('/mob/living/forceMove')
    check(lf is not None and lf.layer is not None and lf.witness[0] == 'call' and lf.witness[1] == 1972
          and lf.witness[2].display() == '/obj/structure/punji_sticks/unbuckle_mob',
          'living.dm:1972 forceMove -> buckled.unbuckle_mob(force = TRUE) -> punji_sticks (review chain)')
    check(sleeps('/obj/structure/punji_sticks/unbuckle_mob') and sleeps('/obj/golfcart_rear/doMove')
          and sleeps('/obj/machinery/atmospherics/update_icon') and sleeps('/obj/machinery/harvester/Exited')
          and sleeps('/obj/machinery/dna_scannernew/container_resist_act'),
          'review links may sleep: punji unbuckle_mob, golfcart doMove, atmos update_icon, harvester Exited, '
          'dna scanner container_resist_act')
    hx = d1('/obj/machinery/harvester/Exited')
    hsite = [st for st in a.sites if st.caller is hx and st.line == 186 and st.text == 'container_resist_act']
    htargets = [t.display() for key, mode, ty in (hsite[0].targets if hsite else ()) for t in a.target_defs(key, mode, ty)]
    check(htargets == ['/obj/machinery/harvester/container_resist_act'],
          'harvester.dm:186 container_resist_act(gone) on src reaches only the harvester override '
          '(the review linked it to dna_scanner by name; the slot is shared, the dispatch is not)')
    check(a.slot_witness(slot('/atom/movable', 'forceMove')).startswith(
        '/mob/living/forceMove (code/modules/mob/living/living.dm:1972) -> /obj/structure/punji_sticks/unbuckle_mob'),
        'forceMove witness is the review chain')
    for root, name in (('/atom/movable', 'Moved'), ('/atom/movable', 'forceMove')):
        w = a.slot_witness(slot(root, name))
        check(w.endswith(('-> sleep', '-> alert', '-> input', '-> winexists', '-> winget')),
              '%s/%s witness ends in a sleep primitive' % (root, name))

    # -- locals are typed by the declaration in scope (wave-0 coloring review) ---------------------------
    ct = d1('/datum/effect_system/fluid_spread/smoke/freezing/chill_turf')
    check(ct is not None and ct.layer is not None and ct.witness[0] == 'call' and ct.witness[1] == 321
          and ct.witness[2].display() == '/obj/item/burner/extinguish',
          'effects_smoke.dm:321 potential_tinder.extinguish() uses the second declaration '
          '(var/obj/item/potential_tinder), not the first (var/mob/living/potential_tinder)')
    md = d1('/obj/machinery/module_duplicator/ui_static_data')
    check(md is not None and md.layer is None,
          'component_printer.dm:527 materials.mat_container is the src var: the local var/list/materials '
          'is declared only at :534')

    sb = d1('/obj/machinery/power/singularity_beacon/syndicate/no_escape/attack_hand')
    check(sb is not None and sb.layer is not None and sb.phase == 1 and sb.witness[0] == 'call'
          and sb.witness[2].display() == '/obj/machinery/power/singularity_beacon/syndicate/no_escape/Activate',
          "syndicatebeacon.dm:137 'active ? Deactivate(user) : Activate(user)': the ':' is a ternary, "
          'Activate is called on src')
    at = d1('/obj/item/mecha_parts/mecha_equipment/air_tank/process_pump')
    check(at is not None and at.layer is None,
          "air_tank.dm:95 '? internal_tank.air_contents : local_turf.return_air()' calls return_air on the turf")
    ce = d1('/proc/init_chemical_side_effects')
    check(ce is not None and ce.layer is None and not any(
        st.caller is ce and st.text == 'effect' for st in a.sites),
        "reagents.dm:239 'new effect()' creates the type held by effect; it is not a call of a proc named effect")
    cr = d1('/turf/closed/wall/concrete/reinforced/rcd_act')
    check(cr is not None and cr.layer is not None and cr.witness[0] == 'call'
          and cr.witness[2].display() == '/turf/closed/wall/r_wall/rcd_act',
          'concrete_walls.dm:116 call(src, /turf/closed/wall/r_wall::rcd_act())() runs the r_wall definition')

    # -- sync-mandated contexts (W2) -----------------------------------------------------------------------
    check(not any(d.sm and d.layer is not None for d in a.defs), 'no sync-mandated definition is coloured')
    r = nosleep('code/game/atom/atom_appearance.dm', 27, '/atom/update_appearance', 'update_icon')
    check(r == ('SHOULD_NOT_SLEEP', True), 'update_appearance -> update_icon (atom_appearance.dm:27) is a NoSleep conflict')
    r = nosleep('code/game/atom/atoms_initializing_EXPENSIVE.dm', 133, '/atom/Initialize', 'add_atom_colour')
    check(r is not None and r[0] == 'SHOULD_NOT_SLEEP', '/atom/Initialize -> add_atom_colour (:133) is a NoSleep site')
    check(nosleep('code/controllers/master.dm', 902, '/datum/controller/master/RunQueue', 'ignite') is None,
          'waiting-model.md 4.2: RunQueue -> queue_node.ignite (master.dm:902) is a plain call, not a NoSleep '
          'site (ignite is coloured but waitfor = FALSE, so its signature is synchronous)')
    rows = color_procs.nosleep_rows(a)
    check(all(not (row[5] == 'sync' and row[4] == 'yes') for row in rows),
          'an uncoloured (sync) caller never has a call that may suspend it')

    # -- detached code and callbacks do not colour -------------------------------------------------------------
    check(not sleeps('/proc/qdel') and not sleeps('/datum/_SendSignal'), 'qdel and _SendSignal are not coloured')
    check(not sleeps('/proc/addtimer'), 'addtimer is not coloured (CALLBACK creation is not a call)')
    f = flags('/datum/callback', 'Invoke')
    check(f['colored'] and f['suspends'] and not f['lower'],
          'callback Invoke is coloured only by the upper bound (call()() with an unknown target)')

    # -- type order (object-model.md section 9) ----------------------------------------------------------------
    tree = color_procs.type_tree(a)
    order = tree.order
    pos = {t: i for i, t in enumerate(order)}
    check(len(order) == len(pos) > 40000, 'type order lists every type once (%d)' % len(order))
    check(set(order) >= set(a.p.types) | set(typeorder.DMSTANDARD_TYPES),
          'type order contains every mentioned type and every builtin type')
    chain = ['/datum', '/atom', '/atom/movable', '/obj', '/obj/item', '/obj/item/stack']
    check(all(pos[x] < pos[y] for x, y in zip(chain, chain[1:])),
          'type order: ' + ' before '.join(chain))
    check(tree.parent['/obj'] == '/atom/movable' and tree.parent['/mob'] == '/atom/movable'
          and tree.parent['/turf'] == '/atom' and tree.parent['/area'] == '/atom'
          and tree.parent['/atom'] == '/datum' and tree.parent['/client'] == '/datum'
          and tree.parent['/mutable_appearance'] == '/image' and tree.parent['/datum'] == '',
          'builtin parents: /obj and /mob under /atom/movable, /turf and /area under /atom, /atom and '
          '/client (tg parent_type, client_defines.dm:36) under /datum, /mutable_appearance under /image')
    check(tree.children[''] == ['/world', '/savefile', '/datum', '/alist', '/callee', '/list', '/vector'],
          'root children: tg mentions /world (genesis_call.dme), /savefile (__byond_version_compat.dm:18) and '
          '/datum first; OpenDream\'s DMStandard adds /alist, /callee, /list, /vector last')
    check(tree.children['/atom'] == ['/atom/movable', '/turf', '/area'],
          '/atom children: the /atom/movable path child first, then /turf and /area (one-segment types get '
          'their OpenDream ids after the /atom subtree)')
    check(tree.parent.get('/datum/tgs_api/latest') == '/datum/tgs_api/v5',
          'tg parent_type: /datum/tgs_api/latest is a child of /datum/tgs_api/v5 (tgs/core/datum.dm:24)')
    stack = []
    valid = True
    for t in order:
        p = tree.parent[t]
        while stack and stack[-1] != p:
            stack.pop()
        if p and not stack:
            valid = False
            break
        stack.append(t)
    check(valid, 'type order is a depth-first preorder of the type tree (every type follows its parent, '
                 'subtrees are contiguous)')
    first = {t: i for i, t in enumerate(a.p.types)}
    bad = [p for p, ch in tree.children.items()
           if [c for c in ch if c.startswith(p + '/') and c in first]
           != sorted((c for c in ch if c.startswith(p + '/') and c in first), key=first.__getitem__)]
    check(not bad, 'children under their path prefix are in order of first declaration' +
          (' (%s)' % ', '.join(bad[:3]) if bad else ''))
    # Independent of the parse: the first line in .dme include order that declares a type at
    # column 0 ('/obj/machinery/x', '/obj/machinery/x/proc/..', '/obj/machinery/x/var/..').
    fam = '/obj/machinery'
    kids = [c for c in tree.children[fam] if c.startswith(fam + '/')]
    decl = re.compile(r'^(%s/[A-Za-z_]\w*)(?=[/({\s]|$)' % re.escape(fam), re.M)
    seen = {}
    for k, rel in enumerate(pre.files):
        with open(os.path.join(tg, rel), encoding='utf-8', errors='replace') as fh:
            text = fh.read()
        if fam not in text:
            continue
        for m in decl.finditer(text):
            seen.setdefault(m.group(1), (k, text.count('\n', 0, m.start())))
    found = [c for c in kids if c in seen]
    check(len(found) > 0.9 * len(kids) and found == sorted(found, key=seen.__getitem__),
          'the %d direct children of %s found by a plain text scan are in the order of their first column-0 '
          'declaration in .dme include order (%d of %d children)' % (len(found), fam, len(found), len(kids)))
    src = open(os.path.join(color_procs.REPO, color_procs.TYPE_ORDER_CS), encoding='utf-8').read()
    m = re.search(r'Encoded\(\) => """\n(.*?)\n""";', src, re.S)
    check(m is not None and typeorder.decode_tree(m.group(1)) == order
          and ('public const int Count = %d;' % len(order)) in src,
          'TypeOrder.g.cs decodes to the type order')
    procs = typeorder.proc_order(a.defs, typeorder.enumerated_proc_types(a.defs))
    gv = procs.get(('/datum/controller/global_vars', 'proc'), [])
    gvars = set(a.p.type_vars.get('/datum/controller/global_vars', {})) - {
        'gvars_datum_protected_varlist', 'gvars_datum_in_built_vars', 'gvars_datum_init_order'}
    check(list(procs) == [('/datum/controller/global_vars', 'proc')] and len(gv) > 500
          and sorted(n[len('InitGlobal'):] for n in gv) == sorted(gvars),
          'ProcOrder: only /datum/controller/global_vars is enumerated (globals.dm:51), and its procs are one '
          'InitGlobal<var> per global var, as globals.dm:53 checks (%d)' % len(gv))

    # -- slot registry (translation-rules.md v1) --------------------------------------------------------------
    reg = color_procs.slot_registry(a)

    def sig(root, name, dmtask=False):
        r = reg.sigs.get((root, name))
        return reg.signature(r, dmtask=dmtask) if r else None
    rules = color_procs.read_rules()
    expected = {(r[0], r[1]): r[3] for r in slotreg.appendix_a(rules) if 'override' not in r[3].split('(')[0]}
    for key in (('/datum', 'New'), ('/datum', 'Destroy'), ('/datum', 'process'), ('/atom', 'Initialize'),
                ('/atom', 'LateInitialize'), ('/atom', 'Entered'), ('/atom', 'Exited'), ('/atom', 'Exit'),
                ('/atom', 'Bumped'), ('/atom', 'CanPass'), ('/atom', 'CanAllowThrough'), ('/atom', 'setDir'),
                ('/atom/movable', 'Move'), ('/atom/movable', 'Moved'), ('/atom/movable', 'forceMove'),
                ('/atom/movable', 'doMove'), ('/atom/movable', 'abstract_move'), ('/atom/movable', 'Bump'),
                ('/datum/controller', 'Initialize'), ('/datum/controller/subsystem', 'fire'),
                ('/datum/component', 'Initialize'), ('/datum/element', 'Attach'), ('/datum/element', 'Detach'),
                ('/obj/item', 'use')):
        check(sig(*key) == expected.get(key), 'Appendix A signature of %s %s: %s' % (key[0], key[1], sig(*key)))
    ign = reg.sigs[('/datum/controller/subsystem', 'ignite')]
    check(sig('/atom/movable', 'Move', dmtask=True).startswith('public virtual DmTask<object> Move(')
          and sig('/atom', 'setDir', dmtask=True).startswith('public virtual DmTask setDir(')
          and ign.slot.colored and not ign.slot.suspends
          and 'DmTask' not in reg.signature(ign, dmtask=ign.slot.suspends),
          'DmTask exactly when callers must wait (waiting-model.md 4.2): Move and setDir are DmTask, ignite '
          '(coloured, waitfor = FALSE) stays synchronous')
    check(sig('', 'qdel') == 'public static void qdel(object to_delete = null, float? force = null)',
          'translation-rules.md 7.8 example: qdel(object to_delete, float? force); to_delete is object by 3.4')
    check(sig('', 'sortTim') == 'public static object sortTim(DmList to_sort = null, string cmp = null, float? associative = null, '
          'float? fromIndex = null, float? toIndex = null)'
          and sig('', 'sort_list') == 'public static object sort_list(DmList list_to_sort = null, string cmp = null)'
          and (sig('/obj/item/circuit_component', 'add_option_port') or '').startswith(
              'public virtual object add_option_port(object name = null, DmList list_to_use = null, float? order = null, string trigger = null'),
          'translation-rules.md 7.7, 3.3 P1/P4: a proc reference default is text, not a typepath: sortTim(cmp = '
          'GLOBAL_PROC_REF(cmp_numeric_asc)), sort_list(cmp=/proc/cmp_text_asc) and add_option_port(trigger = '
          'PROC_REF(input_received)) take a string')
    check(sig('/atom', 'build_appearance_list') == 'public virtual object build_appearance_list(object build_overlays = null)',
          'translation-rules.md 3.4 example: build_appearance_list(list/build_overlays) wraps a non-list, so object')
    check(sig('/atom', 'can_atmos_pass') == 'public virtual object can_atmos_pass__proc(turf target_turf = null, float? vertical = null)',
          'translation-rules.md 2.4 example: can_atmos_pass__proc(turf target_turf, float? vertical)')
    st = reg.sigs[('/obj/item', 'use')]
    stack_use = next(d for d in st.slot.defs if d.type == '/obj/item/stack')
    check(st.variadic and st.dmparams_overrides >= 1 and
          [p.name for p in slotreg.dmscan.parse_params(stack_use.args)] == ['used', 'transfer', 'check'],
          'translation-rules.md 7.2 example: /obj/item/stack/use(used, transfer, check) makes /obj/item/use '
          'variadic and needs [DmParams]')
    wound = sig('', 'get_corresponding_wound_type') or ''
    check(wound.startswith('public static object get_corresponding_wound_type(object wounding_type = null, '
                           'obj_item_bodypart part = null, object severity_min = null, object severity_max = null, float? '),
          'P5: severity_max = severity_min (non-constant default) keeps the P2 type object (__DEFINES/wounds.dm:228)')
    check(sig('/datum/timed_action', 'await', dmtask=True) == 'public virtual DmTask<object> @await(object delay = null)'
          and '@override' in sig('/datum', 'RegisterSignal'),
          '2.2: the DM name await is @await, the parameter override is @override')
    check(reg.sigs[('/client', 'New')].hides and sig('/client', 'New').startswith('public new virtual'),
          '7.1: /client/New hides /datum/New (public new virtual)')
    check(reg.classes.of('/obj/projectile/energy/tesla_cannon') == 'obj_projectile_energy_tesla_cannon__2'
          and reg.classes.of('/obj/projectile/energy/tesla/cannon') == 'obj_projectile_energy_tesla_cannon'
          and len(reg.classes.collisions) == 2,
          'B.1: two class-name collisions; the path with fewer segments gets __2')
    check(reg.sigs[('/datum', 'New')].returns == 'void' and reg.sigs[('/datum', 'Destroy')].returns == 'object'
          and reg.sigs[('/atom', 'setDir')].returns == 'void',
          '7.4: /datum/New and setDir are void, Destroy returns a value')
    check((sig('', 'do_after', dmtask=True) or '').startswith(
        'public static DmTask<object> do_after(atom_movable user = null, object delay = null, object target = null'),
          '3.4: do_after\'s atom/target is object (timed_actions.dm:223 iterates it when it is a list)')
    check((sig('/datum/reagents', 'has_reagent') or '').startswith(
        'public virtual object has_reagent(object target_reagent = null, ')
          and (sig('/mob/living/carbon', 'set_blood_type') or '').startswith(
              'public virtual void set_blood_type(object new_blood_type = null, '),
          '3.4 with short-circuit conditions: has_reagent(datum/reagent/target_reagent) returns when '
          '"!isnull(P) && !ispath(P)" and then compares the typepath (holder.dm:684, 694); set_blood_type converts '
          'text or a path in the body of "istext(P) || ispath(P, /datum/blood_type)" (carbon.dm:1218-1219)')
    check(any(u.startswith('added_layer: Type?') for u in reg.sigs[('/area', 'place_on_top_react')].undecided),
          'P1: place_on_top_react(turf/added_layer) receives a turf type (baseturfs.dm:38) and the shuttle override '
          'tests ispath(added_layer, /turf/open/floor/plating) (areas/shuttles.dm:23): listed as undecided')
    check(not any(u.startswith('runlevel_SS') for u in reg.sigs[('/datum/controller/master', 'SoftReset')].undecided),
          '3.4: an earlier unknown term of "A || !istype(P)" can send a list into the body too, so the log message '
          'there is shared code (master.dm:965-966)')

    # -- witness chains in colored-slots.tsv expand to the full chains ---------------------------------------------
    srows = color_procs.slots_rows(a)
    by_slot = {r[0] + '/' + r[1]: r[7] for r in srows}
    bad = [s.display() for s in a.slots.values() if s.colored and s.tg_defs()
           and color_procs.expand_witness(by_slot, by_slot[s.display()]) != a.slot_witness(s)]
    check(not bad, 'every shortened witness chain of colored-slots.tsv expands to the full chain' +
          (' (%s)' % ', '.join(bad[:3]) if bad else ''))

    # -- bounds and determinism ----------------------------------------------------------------------------------
    check(all(d.phase in (1, 2) for d in a.defs if d.layer is not None), 'every coloured def has a phase')
    sd = a.slot_dispatch_counts
    check(sd[0] >= a.counts[0] and sd[1] >= a.counts[1],
          'dispatch narrowing only removes colour (slot-level dispatch: %d slots, narrowed: %d)' % (sd[0], a.counts[0]))
    files, _ = color_procs.outputs(a, pre, color_procs.tg_revision(tg))
    stale = []
    for rel, content in sorted(files.items()):
        path = os.path.join(color_procs.REPO, rel)
        if os.path.exists(path):
            with open(path, encoding='utf-8', newline='') as fh:
                if fh.read() != content:
                    stale.append(rel)
        else:
            stale.append(rel)
    check(not stale, 'committed generated files are up to date' + (' (stale: %s)' % ', '.join(stale) if stale else ''))

    # A second type tree and slot registry over the same parse give the same output.
    tree2 = typeorder.TypeTree(a.p.types, a.p.parent_type_decls)
    check(typeorder.render_type_order(tree2, 'x') == typeorder.render_type_order(tree, 'x'),
          'a second type tree of the same parse gives byte-identical TypeOrder.g.cs')
    reg2 = slotreg.Registry(a, tree2)
    flags = lambda s: color_procs.slot_flags(a, s)  # noqa: E731
    raw = slotreg.RawHeaders(tg)
    check(slotreg.tsv_rows(reg2, raw, flags) == slotreg.tsv_rows(reg, raw, flags),
          'a second slot registry of the same parse gives identical rows')

    # Last: a second Analysis over the same parse takes over the per-definition state.
    text1 = color_procs.render_tsv(color_procs.SLOTS_HEADER, color_procs.slots_rows(a))
    b = coloring.Analysis(a.p, coloring.Options()).run()
    text2 = color_procs.render_tsv(color_procs.SLOTS_HEADER, color_procs.slots_rows(b))
    check(text1 == text2, 'a second analysis of the same parse gives byte-identical output')

    dt = time.time() - t0
    check(dt < 60, 'ran in %.1f s (limit 60 s)' % dt)
    print('%d passed, %d failed' % (len(PASSES), len(FAILS)))
    return 1 if FAILS else 0


if __name__ == '__main__':
    sys.exit(main())
