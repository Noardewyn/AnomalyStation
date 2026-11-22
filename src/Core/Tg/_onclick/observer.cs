// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/observer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the ghost's double click (M10x slice C, D281: follow a movable, jump to a turf) and its single click with tg's
// attack_ghost (the engineering package E2, D287: examine, a machine's window to observers such as the supermatter's, the gas,
// health and chemical scans). Left out: the admin ghost's clicks (live-partial.txt "Ghosts", docs/ai/specs/not-translated.md section
// 16; isAdminGhostAI reads FALSE, as in tgui's states.cs) and the inert types' own attack_ghost (the other machines and structures
// of later milestones). Gaps TP1: the teleporter hub's (:76), clicked through to its target.

// tg: code/_onclick/observer.dm:1
public partial class mob_dead_observer : mob_dead
{
    // tg: code/_onclick/observer.dm:1
    public override async DmTask DblClickOn(atom A = null, object @params = null)
    {
        if (Dm.truthy(await check_click_intercept(@params, A)))
            return;

        // Things you might plausibly want to follow
        if (ismovable(A))
            await ManualFollow((atom_movable)A);

        // Otherwise jump
        else if (Dm.truthy(A.loc))
            await abstract_move(get_turf(A));
    }

    // tg: code/_onclick/observer.dm:13
    public override async DmTask ClickOn(atom A = null, object @params = null)
    {
        if (Dm.truthy(await check_click_intercept(@params, A)))
            return;

        DmList modifiers = params2list(@params);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_CLICKON]) ? (object)NONE : this._SendSignal(COMSIG_MOB_CLICKON, Dm.list(this, A, modifiers))), COMSIG_MOB_CANCEL_CLICKON)))
            return;

        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)))
        {
            if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
            {
                await ShiftMiddleClickOn(A);
                return;
            }
            if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
            {
                await CtrlShiftClickOn(A);
                return;
            }
            await ShiftClickOn(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
        {
            if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
                await CtrlMiddleClickOn(A);
            else
                MiddleClickOn(A, @params);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK)))
        {
            await base_click_alt(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
        {
            await CtrlClickOn(A);
            return;
        }

        if (world.time <= Dm.num(next_move))
            return;
        // You are responsible for checking config.ghost_interaction when you override this function
        // Not all of them require checking, see below
        await A.attack_ghost(this);
    }
}

// Oh by the way this didn't work with old click code which is why clicking shit didn't spam you
public partial class atom
{
    // tg: code/_onclick/observer.dm:50
    public virtual async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACK_GHOST]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ATTACK_GHOST, Dm.list(this, user))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        if (Dm.truthy(user.client))
        {
            if (Dm.truthy(Dm.bitand(user.ghost_hud_flags, GHOST_GAS)) && Dm.truthy(atmos_scan(user: user, target: this, silent: TRUE)))
                return TRUE;
            // else if(isAdminGhostAI(user)) attack_ai(user): the admin ghost's AI interaction (admin tooling, M10+; FALSE as in states.cs)
            else if (Dm.truthy(user.client.prefs.read_preference(typeof(datum_preference_toggle_inquisitive_ghost))))
                user.examinate(this);
        }
        return FALSE;
    }
}

public partial class mob_living
{
    // tg: code/_onclick/observer.dm:62
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (user.client == null)
            return dot;

        if (Dm.truthy(Dm.bitand(user.ghost_hud_flags, GHOST_HEALTH)))
            healthscan(user, this, SCANNER_VERBOSE, SCANPOWER_SUPER);
        if (Dm.truthy(Dm.bitand(user.ghost_hud_flags, GHOST_CHEM)))
            chemscan(user, this);
        return dot;
    }
}

// tg: code/game/machinery/teleporter.dm:1
public partial class obj_machinery_teleport_hub : obj_machinery_teleport
{
    // Now you can click through portals, wormholes, gateways, and teleporters while observing. -Sayu
    // tg: code/_onclick/observer.dm:76
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (!Dm.truthy(power_station?.engaged) || power_station.teleporter_console == null || power_station.teleporter_console.target_ref == null)
            return await base.attack_ghost(user);

        atom target = power_station.teleporter_console.target_ref.resolve() as atom;
        if (target == null)
        {
            power_station.teleporter_console.target_ref = null;
            return await base.attack_ghost(user);
        }

        await user.abstract_move(get_turf(target));
        return null;
    }
}
