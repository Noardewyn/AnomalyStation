// Translated from tgstation (snapshot 1d99bd7f): code/datums/dash_weapon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/// Actions that you can use to dash (teleport) to places in view.
// tg: code/datums/dash_weapon.dm:2
public partial class datum_action_innate_dash : datum_action_innate
{
    // tg: code/datums/dash_weapon.dm:26
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (current_charges <= 0)
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("no charges!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/datums/dash_weapon.dm:36
    public override async DmTask<object> Activate()
    {
        if (target is not obj_item dashing_item)
            return null;

        await dashing_item.attack_self(owner); //Used to toggle dash behavior in the dashing item
        return null;
    }

    /// Teleports user to target using do_teleport. Returns TRUE if teleport successful, FALSE otherwise.
    // tg: code/datums/dash_weapon.dm:44
    public virtual async DmTask<object> teleport(mob user = null, atom target = null)
    {
        if (!Dm.truthy(await IsAvailable(feedback: TRUE)))
            return FALSE;

        turf current_turf = get_turf(user);
        turf target_turf = get_turf(target);
        // A mob with no client reads the default view range here, where tg would runtime.
        if (!Dm.@in(target, view(user.client?.view, user)))
        {
            user.balloon_alert(user, I18n.T("out of view!"));
            return FALSE;
        }
        if (Dm.truthy(target_turf.is_blocked_turf_ignore_climbable()))
        {
            user.balloon_alert(user, I18n.T("destination blocked!"));
            return FALSE;
        }
        if (!Dm.truthy(await do_teleport(user, target_turf, no_effects: TRUE)))
        {
            user.balloon_alert(user, I18n.T("dash blocked!"));
            return FALSE;
        }

        // Note: It's possible do_teleport, for whatever reason,
        // caused our owner to be unassigned by this point.
        // (Such as dropping our item after landing.)

        obj spot_one = Dm.@new(phaseout, current_turf, user.dir) as obj;
        obj spot_two = Dm.@new(phasein, target_turf, user.dir) as obj;
        spot_one.Beam(spot_two, beam_effect, time: beam_length);
        playsound(target_turf, dash_sound, 25, TRUE);
        current_charges--;
        _addtimer(Dm.@new<datum_callback>(this, nameof(charge)), charge_rate, file: "code/datums/dash_weapon.dm", line: 70);
        if (owner != null)
            await owner.update_mob_action_buttons();
        SEND_SIGNAL(COMSIG_DASH_ACTION_DASHED, Dm.list(this));

        return TRUE;
    }

    /// Callback for [/proc/teleport] to increment our charges after  use.
    // tg: code/datums/dash_weapon.dm:76
    public virtual async DmTask charge()
    {
        current_charges = clamp(current_charges + 1, 0, max_charges);
        SEND_SIGNAL(COMSIG_DASH_ACTION_CHARGED, Dm.list(this));

        if (target is not obj_item dashing_item)
            return;

        if (Dm.truthy(recharge_sound))
            playsound(dashing_item, recharge_sound, 50, TRUE);

        if (owner == null)
            return;
        await owner.update_mob_action_buttons();
        dashing_item.balloon_alert(owner, I18n.T("[]/[] dash charges", current_charges, max_charges));
    }
}
