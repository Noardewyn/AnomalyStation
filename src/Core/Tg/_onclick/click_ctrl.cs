// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/click_ctrl.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (docs/ai/specs/m4a-ownership.md). Pulling (2026-10-15): the living mob's grab and pulled are translated
// (living_defense.cs, living.cs); the admin's datum tag is a rule-1.4 block (M10). The interaction guard of D132 is at the top of the
// target's click_ctrl and click_ctrl_shift bases (docs/ai/specs/interaction.md 7, DV27): a refusal is CLICK_ACTION_BLOCKING.

// tg: code/_onclick/click_ctrl.dm:4
public partial class mob : atom_movable
{
    /**
     * Ctrl click
     */
    // tg: code/_onclick/click_ctrl.dm:4
    public virtual async DmTask CtrlClickOn(atom A = null)
    {
        await base_click_ctrl(A);
    }

    /**
     * ### Base proc for ctrl click interaction left click.
     *
     * If you wish to add custom `click_ctrl` behavior for a single type, use that proc.
     */
    // tg: code/_onclick/click_ctrl.dm:12
    public virtual async DmTask<object> base_click_ctrl(atom target = null)
    {
        // Check if they've hooked in to prevent src from ctrl clicking anything
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_CTRL_CLICKED]) ? (object)NONE : this._SendSignal(COMSIG_MOB_CTRL_CLICKED, Dm.list(this, target))), COMSIG_MOB_CANCEL_CLICKON)))
            return TRUE;

        // If it has a signal handler that returns a click action, done.
        if (Dm.truthy(Dm.bitand((!Dm.truthy(target._listen_lookup?[COMSIG_CLICK_CTRL]) ? (object)NONE : target._SendSignal(COMSIG_CLICK_CTRL, Dm.list(target, this))), CLICK_ACTION_ANY)))
            return TRUE;

        // If it has a custom click_alt that returns success/block, done.
        if (Dm.truthy(await can_perform_action(target, Dm.bitor(target.interaction_flags_click, SILENT_ADJACENCY))))
            return Dm.bitand(await target.click_ctrl(this), CLICK_ACTION_ANY);

        return FALSE;
    }
}

// tg: code/_onclick/click_ctrl.dm:33
public partial class mob_living : mob
{
    /**
     * Ctrl click
     * For most objects, pull
     */
    // tg: code/_onclick/click_ctrl.dm:33
    public override async DmTask<object> base_click_ctrl(atom target = null)
    {
        object dot = null;
        dot = await base.base_click_ctrl(target);
        if (Dm.truthy(dot) || !ismovable(target) || world.time < Dm.num(next_move) || !Dm.truthy(await can_perform_action(target, Dm.bitor(Dm.bitor(Dm.bitor(NOT_INSIDE_TARGET, SILENT_ADJACENCY), ALLOW_RESTING), FORBID_TELEKINESIS_REACH))))
            return dot;

        dot = TRUE;
        // DEVIATION(DV66): a ctrl-click on what the mob already pulls lets go of it (tg: nothing for a thing, a stronger grab for a mob,
        // which a mob in combat mode still gets); a ctrl-click on another thing pulls that one instead, as tg's start_pulling does.
        if (target == pulling && !(target is mob_living && Dm.truthy(combat_mode)))
        {
            await stop_pulling();
            return dot;
        }
        if (!Dm.eq(await grab(target as mob_living), GRAB_SKIP))
        {
            changeNext_move(CLICK_CD_MELEE);
            return dot;
        }
        pulled(target as atom_movable);
        return dot;
    }
}

// tg: code/_onclick/click_ctrl.dm:50
public partial class mob : atom_movable
{
    /**
     * Ctrl mouse wheel click
     * Except for tagging datumns same as control click
     */
    // tg: code/_onclick/click_ctrl.dm:50
    public virtual async DmTask CtrlMiddleClickOn(atom A = null)
    {
        // tg: :51  if(check_rights_for(client, R_ADMIN))
        // GAP(lead): check_rights_for (admin/holder.dm) is not declared; its first test is the client's admin holder, which
        // no client has before the admin tools (M10).
        if (Dm.truthy(client?.holder))
        {
            Dm.NotTranslated("code/_onclick/click_ctrl.dm:51", "CtrlMiddleClickOn: check_rights_for(client, R_ADMIN) and client.toggle_tag_datum(A) (the admin's datum tag, M10+)");
            return;
        }
        await CtrlClickOn(A);
    }
}

// tg: code/_onclick/click_ctrl.dm:68
public partial class atom : datum
{
    /**
     * ## Custom ctrl click interaction
     * Override this to change default ctrl click behavior. Return `CLICK_ACTION_SUCCESS`, `CLICK_ACTION_BLOCKING` or `NONE`.
     *
     * ### Guard clauses
     * Consider adding `interaction_flags_click` before adding unique guard clauses.
     *
     * ### Return flags
     * Forgetting your return will cause the default ctrl click behavior to occur thereafter.
     *
     * Returning any value besides NONE will stop the attack chain and thus stop the object from getting pulled/grabbed
     **/
    // tg: code/_onclick/click_ctrl.dm:68
    public virtual async DmTask<object> click_ctrl(mob user = null)
    {
        // DEVIATION(DV27): the interaction guard (D132).
        if (InertSeam.MissingOverride(this, "click_ctrl", user))
            return CLICK_ACTION_BLOCKING;
        return NONE;
    }
}

// tg: code/_onclick/click_ctrl.dm:77
public partial class mob : atom_movable
{
    /**
     * Control+Shift click
     * Unused except for AI
     */
    // tg: code/_onclick/click_ctrl.dm:77
    public virtual async DmTask CtrlShiftClickOn(atom A = null)
    {
        await base_click_ctrl_shift(A);
    }

    /**
     * ### Base proc for ctrl shift click interaction left click.
     *
     * If you wish to add custom `click_ctrl_shift` behavior for a single type, use that proc.
     */
    // tg: code/_onclick/click_ctrl.dm:85
    public virtual async DmTask base_click_ctrl_shift(atom target = null)
    {
        // Check if they've hooked in to prevent src from ctrl clicking anything
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_CTRL_SHIFT_CLICKED]) ? (object)NONE : this._SendSignal(COMSIG_MOB_CTRL_SHIFT_CLICKED, Dm.list(this, target))), COMSIG_MOB_CANCEL_CLICKON)))
            return;

        // If it has a signal handler that returns a click action, done.
        if (Dm.truthy(Dm.bitand((!Dm.truthy(target._listen_lookup?[COMSIG_CLICK_CTRL_SHIFT]) ? (object)NONE : target._SendSignal(COMSIG_CLICK_CTRL_SHIFT, Dm.list(target, this))), CLICK_ACTION_ANY)))
            return;

        // Proceed with ctrl shift click
        if (Dm.truthy(await can_perform_action(target, Dm.bitor(target.interaction_flags_click, SILENT_ADJACENCY))))
            await target.click_ctrl_shift(this);
    }
}

// tg: code/_onclick/click_ctrl.dm:106
public partial class atom : datum
{
    /**
     * ## Custom ctrl shift click interaction
     *
     * ### Guard clauses
     * Consider adding `interaction_flags_click` before adding unique guard clauses.
     **/
    // tg: code/_onclick/click_ctrl.dm:106
    public virtual async DmTask<object> click_ctrl_shift(mob user = null)
    {
        // DEVIATION(DV27): the interaction guard (D132).
        if (InertSeam.MissingOverride(this, "click_ctrl_shift", user))
            return CLICK_ACTION_BLOCKING;
        return NONE;
    }
}
